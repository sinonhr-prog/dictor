using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using Dictor.Audio;
using Dictor.Recognition;
using Dictor.Sessions;
using Dictor.Settings;
using Dictor.Windows;

namespace Dictor.UI;

internal sealed class DictorApp : IDisposable
{
    private readonly string root = AppContext.BaseDirectory;
    private readonly Capsule capsule = new();
    private readonly Engine engine = new();
    private readonly SessionState state = new();
    private readonly System.Windows.Forms.NotifyIcon tray;
    private readonly DispatcherTimer meter = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch duration = new();
    private readonly DispatcherTimer hide = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly AutomationFocusChangedEventHandler focusHandler;
    private Preferences preferences;
    private Capture? capture;
    private Target? target;
    private CancellationTokenSource? cancellation;
    private Task active = Task.CompletedTask;
    private SettingsWindow? settings;
    private string? lastResult;
    private nint handle;
    private int hotkeyId = 1;
    private bool exiting;
    private readonly bool acceptance;
    private bool acceptanceUsed;
    private System.Windows.Controls.TextBox? testField;
    private System.Windows.Window? testWindow;
    private string SettingsPath => Path.Combine(root, "data", "settings.json");

    private DictorApp(bool acceptance)
    {
        this.acceptance = acceptance;
        preferences = Preferences.Load(SettingsPath, out var recovered);
        handle = new WindowInteropHelper(capsule).EnsureHandle();
        HwndSource.FromHwnd(handle).AddHook(Hook);
        if (!Desktop.RegisterHotKey(handle, hotkeyId, preferences.Modifiers | 0x4000, preferences.Key))
            MessageBox.Show("Горячая клавиша занята. Выберите другую в настройках.");
        capsule.Stop += () => { if (state.Phase == Phase.Recording) Toggle(); else capsule.Hide(); };
        hide.Tick += (_, _) => { hide.Stop(); if (state.Phase == Phase.Ready) capsule.Hide(); };
        meter.Tick += (_, _) =>
        {
            capsule.Update($"Запись · {duration.Elapsed:mm\\:ss}" + (duration.Elapsed.TotalSeconds >= 570 ? " · лимит 10 мин" : ""), capture?.Level ?? 0, true);
            if (acceptance && duration.Elapsed.TotalSeconds >= 10 && state.Phase == Phase.Recording) active = StopAsync();
            if (!acceptance && duration.Elapsed.TotalSeconds >= 599 && state.Phase == Phase.Recording) active = StopAsync();
        };
        focusHandler = (_, _) => capsule.Dispatcher.BeginInvoke(() =>
        {
            if (target is not null && !Desktop.Matches(target, Desktop.Snapshot())) target = null;
        });
        Automation.AddAutomationFocusChangedEventHandler(focusHandler);
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerMode;
        tray = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!), Text = "Диктор", Visible = true, ContextMenuStrip = new() };
        tray.ContextMenuStrip.Items.Add("Начать / остановить", null, (_, _) => Toggle());
        tray.ContextMenuStrip.Items.Add("Настройки", null, (_, _) => ShowSettings());
        tray.ContextMenuStrip.Items.Add("Скопировать снова", null, async (_, _) => { if (lastResult is not null) Show(await Desktop.DeliverAsync(lastResult, null, false, CancellationToken.None)); });
        tray.ContextMenuStrip.Items.Add("Выход", null, async (_, _) => await ExitAsync());
        tray.DoubleClick += (_, _) => ShowSettings();
        if (recovered) MessageBox.Show("Настройки повреждены. Восстановлена резервная копия или значения по умолчанию.");
    }

    public static int RunApp(bool acceptance = false, bool shutdownCheck = false)
    {
        using var mutex = new Mutex(true, "Local\\DictorApplication", out var first);
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DictorShowSettings");
        if (!first) { signal.Set(); return 0; }
        var app = new Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
        using var controller = new DictorApp(acceptance);
        var registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => app.Dispatcher.BeginInvoke(controller.ShowSettings), null, Timeout.Infinite, false);
        app.Startup += async (_, _) => { if (!acceptance && !shutdownCheck) controller.ShowSettings(); await controller.InitializeAsync(); if (shutdownCheck) await controller.ExitAsync(); else if (acceptance) controller.ShowTest(); };
        try { return app.Run(); }
        finally { registration.Unregister(null); mutex.ReleaseMutex(); }
    }

    private async Task InitializeAsync()
    {
        Show("Загружаю модель…");
        active = Task.Run(async () => { engine.Load(Path.Combine(root, "models"), preferences.Cpu); await engine.WarmAsync(); });
        try { await active; state.Ready(); Show("Готов · горячая клавиша начинает запись"); }
        catch { Show("Модель не загружена. Проверьте файлы; для CPU измените профиль и перезапустите."); }
    }

    private nint Hook(nint w, int message, nint p, nint l, ref bool handled)
    {
        if (message == 0x312)
        {
            handled = true;
            if ((int)p == 99) Cancel(); else Toggle();
        }
        return 0;
    }
    private void Toggle()
    {
        if (exiting) return;
        if (settings?.IsTesting == true) return;
        if (state.Phase == Phase.Recording) { active = StopAsync(); return; }
        if (acceptance && acceptanceUsed) return;
        if (!state.Begin()) return;
        if (acceptance) acceptanceUsed = true;
        target = Desktop.Snapshot();
        cancellation = new();
        if (!Desktop.RegisterHotKey(handle, 99, 0x4000, 0x1B))
        {
            state.Ready(); Show("Esc занят другим приложением. Запись не начата."); return;
        }
        try
        {
            capture = new Capture(preferences.Microphone);
            capture.LimitReached += () => capsule.Dispatcher.BeginInvoke(() => { if (state.Phase == Phase.Recording) active = StopAsync(); });
            capture.Start(); duration.Restart(); meter.Start(); Show("Запись", true);
        }
        catch { capture?.Dispose(); capture = null; Finish(); Show("Микрофон недоступен. Проверьте устройство и разрешения Windows."); }
    }

    private async Task StopAsync()
    {
        if (!state.Stop()) return;
        var id = state.Id;
        var token = cancellation!.Token;
        meter.Stop(); Show("Распознаю…");
        try
        {
            var samples = await capture!.StopAsync(); capture.Dispose(); capture = null;
            token.ThrowIfCancellationRequested();
            var text = await Task.Run(() => engine.RecognizeAsync(samples, token), token);
            if (!state.Deliver(id) || token.IsCancellationRequested) return;
            if (string.IsNullOrWhiteSpace(text)) { Show("Речь не обнаружена"); return; }
            lastResult = text;
            Show(await Desktop.DeliverAsync(text, target, preferences.Paste, token));
            if (acceptance)
            {
                await Task.Delay(400);
                File.WriteAllText(Path.Combine(root, "data", "acceptance.json"), System.Text.Json.JsonSerializer.Serialize(new { code = "MICROPHONE_ACCEPTANCE", seconds = duration.Elapsed.TotalSeconds, characters = text.Length, inserted = testField?.Text == text, focusPreserved = Desktop.Matches(target, Desktop.Snapshot()) }));
            }
        }
        catch (OperationCanceledException) { Show("Отменено"); }
        catch { Show("Не удалось завершить диктовку. Проверьте микрофон и модель."); }
        finally { capture?.Dispose(); capture = null; Finish(); }
    }
    private void Cancel()
    {
        if (state.Phase is Phase.Ready or Phase.Starting) return;
        cancellation?.Cancel();
        if (state.Phase == Phase.Recording) active = StopAsync();
        state.Cancel();
        meter.Stop(); Show("Отмена…");
    }
    private void Finish()
    {
        meter.Stop(); Desktop.UnregisterHotKey(handle, 99); cancellation?.Dispose(); cancellation = null; target = null; state.Ready();
        hide.Start();
    }
    private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock) capsule.Dispatcher.BeginInvoke(Cancel);
    }
    private void OnPowerMode(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Suspend) capsule.Dispatcher.BeginInvoke(Cancel);
    }
    private void Show(string text, bool recording = false)
    {
        hide.Stop();
        capsule.Configure(preferences); capsule.Update(text, 0, recording); capsule.Show();
        if (!recording && state.Phase == Phase.Ready) hide.Start();
    }
    private void ShowSettings()
    {
        if (settings is not null) { settings.Show(); settings.Activate(); return; }
        settings = new SettingsWindow(preferences, Save, draft => { if (state.Phase != Phase.Ready) return; capsule.Configure(draft); capsule.Update("Запись · 00:00", 0, true); capsule.Show(); hide.Start(); }, () => state.Phase == Phase.Ready);
        settings.Closed += (_, _) => settings = null;
        settings.Show();
    }
    private void ShowTest()
    {
        Directory.CreateDirectory(Path.Combine(root, "data"));
        testField = new System.Windows.Controls.TextBox { AcceptsReturn = true, Margin = new System.Windows.Thickness(16), Height = 160 };
        var start = new System.Windows.Controls.Button { Content = "Начать один тест — до 10 секунд", Margin = new System.Windows.Thickness(16), Padding = new System.Windows.Thickness(10) };
        var panel = new System.Windows.Controls.StackPanel(); panel.Children.Add(testField); panel.Children.Add(start);
        testWindow = new System.Windows.Window { Title = "Диктор — проверка микрофона", Width = 500, Height = 300, Content = panel };
        start.Click += (_, _) => { testField.Focus(); Toggle(); start.IsEnabled = false; };
        testWindow.Closed += async (_, _) => { if (!exiting) await ExitAsync(); };
        testWindow.Show();
    }
    private bool Save(Preferences next)
    {
        if (state.Phase is not (Phase.Ready or Phase.Starting)) { MessageBox.Show("Сначала завершите диктовку."); return false; }
        var newId = hotkeyId == 1 ? 2 : 1;
        bool changed = next.Key != preferences.Key || next.Modifiers != preferences.Modifiers;
        if (changed && !Desktop.RegisterHotKey(handle, newId, next.Modifiers | 0x4000, next.Key)) { MessageBox.Show("Сочетание занято. Старая клавиша сохранена."); return false; }
        try { next.Save(SettingsPath); }
        catch { if (changed) Desktop.UnregisterHotKey(handle, newId); MessageBox.Show("Не удалось сохранить настройки."); return false; }
        if (changed) { Desktop.UnregisterHotKey(handle, hotkeyId); hotkeyId = newId; }
        preferences = next; return true;
    }
    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true; Cancel();
        try { await active; } catch { }
        tray.Visible = false;
        Application.Current.Shutdown();
    }
    public void Dispose()
    {
        // Remove the shell icon before native cleanup, even if model disposal fails.
        tray.Visible = false; tray.Dispose();
        meter.Stop(); hide.Stop(); Automation.RemoveAutomationFocusChangedEventHandler(focusHandler);
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerMode;
        Desktop.UnregisterHotKey(handle, hotkeyId); Desktop.UnregisterHotKey(handle, 99);
        capture?.Dispose(); engine.Dispose(); capsule.Close(); settings?.Close();
    }
}



