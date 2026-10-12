using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dictor.Settings;
using NAudio.CoreAudioApi;

namespace Dictor.UI;

internal sealed class SettingsWindow : Window
{
    public bool IsTesting { get; private set; }
    private Audio.Capture? levelCapture;
    private readonly System.Windows.Threading.DispatcherTimer levelTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    public SettingsWindow(Preferences current, Func<Preferences, bool> save, Action<Preferences> preview, Func<bool> canTest, bool designPreview = false)
    {
        Title = "Диктор — настройки"; Width = 640; SizeToContent = SizeToContent.Height;
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Dictor.ico"));
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Design.Apply(this, current.Theme);
        var shell = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) };
        shell.SetResourceReference(Border.BackgroundProperty, "Surface"); shell.SetResourceReference(Border.BorderBrushProperty, "Line"); Content = shell;
        var layout = new StackPanel(); shell.Child = layout;
        var header = new DockPanel { Margin = new Thickness(20, 12, 16, 12), LastChildFill = true };
        var close = new Button { Content = "×", FontSize = 22, Padding = new Thickness(8, 0, 8, 2), BorderThickness = new Thickness(0), ToolTip = "Скрыть в трей" };
        System.Windows.Automation.AutomationProperties.SetName(close, "Скрыть настройки в трей");
        close.Click += (_, _) => Close(); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = "Диктор · Настройки", FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock || e.OriginalSource == header) DragMove(); };
        layout.Children.Add(header); layout.Children.Add(Line());
        var panel = new StackPanel { Margin = new Thickness(20, 0, 20, 0) };
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(240, SystemParameters.WorkArea.Height - 190) });
        void Row(string title, FrameworkElement control, string? hint = null, bool divider = true)
        {
            var row = new Grid { Margin = new Thickness(0, 13, 0, 13) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
            labels.Children.Add(new TextBlock { Text = title, FontSize = 13 });
            if (hint is not null) { var small = new TextBlock { Text = hint, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) }; small.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); labels.Children.Add(small); }
            row.Children.Add(labels); Grid.SetColumn(control, 1); row.Children.Add(control); panel.Children.Add(row); if (divider) panel.Children.Add(Line());
            System.Windows.Automation.AutomationProperties.SetName(control, title);
        }
        var microphones = new List<KeyValuePair<string?, string>> { new(null, "Микрофон по умолчанию") };
        if (!designPreview)
        {
            try { using var enumerator = new MMDeviceEnumerator(); foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)) { microphones.Add(new(device.ID, device.FriendlyName)); device.Dispose(); } }
            catch { /* Keep system default selectable when enumeration is unavailable. */ }
        }
        var mic = new ComboBox { ItemsSource = microphones, DisplayMemberPath = "Value", SelectedIndex = Math.Max(0, microphones.FindIndex(x => x.Key == current.Microphone)) };
        var micStack = new StackPanel(); micStack.Children.Add(mic);
        var level = new ProgressBar { Maximum = 1, Height = 4, Margin = new Thickness(0, 8, 0, 8), Visibility = Visibility.Collapsed }; level.SetResourceReference(Control.ForegroundProperty, "Accent"); micStack.Children.Add(level);
        var test = new Button { Content = "Проверить уровень", FontSize = 11, Padding = new Thickness(9, 5, 9, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 0) }; micStack.Children.Add(test);
        Row("Микрофон", micStack, "Выберите устройство записи");
        var elapsed = new System.Diagnostics.Stopwatch(); bool stoppingTest = false;
        async Task StopTest()
        {
            if (!IsTesting || stoppingTest) return; stoppingTest = true; levelTimer.Stop();
            try { await levelCapture!.StopAsync(); }
            catch { }
            finally { levelCapture?.Dispose(); levelCapture = null; IsTesting = false; stoppingTest = false; level.Visibility = Visibility.Collapsed; test.Content = "Проверить уровень"; }
        }
        levelTimer.Tick += async (_, _) => { level.Value = levelCapture?.Level ?? 0; if (elapsed.Elapsed.TotalSeconds >= 10) await StopTest(); };
        test.Click += async (_, _) =>
        {
            if (designPreview) return;
            if (IsTesting) { await StopTest(); return; }
            if (!canTest()) { MessageBox.Show("Дождитесь готовности и завершите диктовку."); return; }
            try { levelCapture = new Audio.Capture(microphones[mic.SelectedIndex].Key); levelCapture.Start(); IsTesting = true; elapsed.Restart(); level.Visibility = Visibility.Visible; levelTimer.Start(); test.Content = "Остановить проверку"; }
            catch { levelCapture?.Dispose(); levelCapture = null; MessageBox.Show("Микрофон недоступен."); }
        };
        Closed += async (_, _) => await StopTest();
        uint modifiers = current.Modifiers, key = current.Key;
        var hotkey = new TextBox { IsReadOnly = true, Text = KeyLabel(modifiers, key), FontFamily = new FontFamily("Consolas"), FontSize = 12, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Нажмите новое сочетание клавиш" };
        hotkey.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Tab) return;
            e.Handled = true; var selected = e.Key == Key.System ? e.SystemKey : e.Key;
            if (selected is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            modifiers = (uint)Keyboard.Modifiers; key = (uint)KeyInterop.VirtualKeyFromKey(selected); hotkey.Text = KeyLabel(modifiers, key);
        };
        Row("Начать / остановить", hotkey, "Нажмите, чтобы изменить сочетание");
        var position = Choice([("Bottom", "Внизу по центру"), ("Top", "Вверху по центру")], current.Position);
        Row("Расположение окна", position);
        var paste = Choice([("paste", "Вставить и скопировать"), ("copy", "Только скопировать")], current.Paste ? "paste" : "copy");
        Row("После диктовки", paste);
        var theme = Choice([("System", "Как в Windows"), ("Light", "Светлая"), ("Dark", "Тёмная")], current.Theme);
        Row("Тема оформления", theme, "Для настроек и капсулы", false);
        theme.SelectionChanged += (_, _) => Design.Apply(this, (string)theme.SelectedValue);
        var advanced = new StackPanel();
        var scale = Choice([("0.75", "Компактный · 75%"), ("1", "Обычный · 100%"), ("1.25", "Крупнее · 125%"), ("1.5", "Крупный · 150%"), ("2", "Очень крупный · 200%")], current.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var monitor = new ComboBox { ItemsSource = Enumerable.Range(1, designPreview ? 1 : System.Windows.Forms.Screen.AllScreens.Length).Select(i => "Экран " + i).ToArray(), SelectedIndex = designPreview ? 0 : Math.Clamp(current.Monitor, 0, System.Windows.Forms.Screen.AllScreens.Length - 1) };
        var profile = Choice([("gpu", "Основной · видеокарта"), ("cpu", "Резервный · процессор")], current.Cpu ? "cpu" : "gpu");
        void Extra(string title, Control control) { var label = new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(0, 12, 0, 5) }; label.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); advanced.Children.Add(label); advanced.Children.Add(control); System.Windows.Automation.AutomationProperties.SetName(control, title); }
        Extra("Размер капсулы", scale); Extra("Монитор", monitor); Extra("Профиль распознавания · после перезапуска", profile);
        var expander = new Expander { Header = "Дополнительно", Content = advanced, Padding = new Thickness(0, 7, 0, 12), FontSize = 12 };
        panel.Children.Add(Line()); panel.Children.Add(expander);
        Preferences Selected() => current with { Microphone = microphones[mic.SelectedIndex].Key, Modifiers = modifiers, Key = key, Paste = (string)paste.SelectedValue == "paste", Cpu = (string)profile.SelectedValue == "cpu", Theme = (string)theme.SelectedValue, Scale = double.Parse((string)scale.SelectedValue, System.Globalization.CultureInfo.InvariantCulture), Position = (string)position.SelectedValue, Monitor = monitor.SelectedIndex };
        var actions = new DockPanel { Margin = new Thickness(0, 10, 0, 18) };
        var button = new Button { Content = "Сохранить", Padding = new Thickness(22, 9, 22, 9), HorizontalAlignment = HorizontalAlignment.Right };
        button.SetResourceReference(Control.BackgroundProperty, "Soft"); button.SetResourceReference(Control.ForegroundProperty, "Accent"); DockPanel.SetDock(button, Dock.Right); actions.Children.Add(button);
        var previewButton = new Button { Content = "Предпросмотр капсулы", HorizontalAlignment = HorizontalAlignment.Left }; previewButton.Click += (_, _) => preview(Selected()); actions.Children.Add(previewButton); panel.Children.Add(actions);
        button.Click += (_, _) =>
        {
            if (modifiers == 0 || key == 0x1B) { MessageBox.Show("Выберите сочетание с Ctrl, Alt, Shift или Win."); return; }
            if (save(Selected())) button.Content = "Сохранено";
        };
        var foot = new Border { Padding = new Thickness(20, 12, 20, 14), CornerRadius = new CornerRadius(0, 0, 16, 16) }; foot.SetResourceReference(Border.BackgroundProperty, "Canvas");
        var note = new TextBlock { Text = "Закрытие настроек скрывает окно в трей. Выход — через меню значка.", FontSize = 11, TextWrapping = TextWrapping.Wrap }; note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); foot.Child = note; layout.Children.Add(foot);
    }
    private static Border Line() { var line = new Border { Height = 1 }; line.SetResourceReference(Border.BackgroundProperty, "Line"); return line; }
    private static ComboBox Choice((string Key, string Label)[] values, string selected) => new() { ItemsSource = values.Select(v => new KeyValuePair<string, string>(v.Key, v.Label)).ToArray(), DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedValue = selected };
    private static string KeyLabel(uint modifiers, uint key)
    {
        var parts = new List<string>(); if ((modifiers & 2) != 0) parts.Add("Ctrl"); if ((modifiers & 1) != 0) parts.Add("Alt"); if ((modifiers & 4) != 0) parts.Add("Shift"); if ((modifiers & 8) != 0) parts.Add("Win"); parts.Add(KeyInterop.KeyFromVirtualKey((int)key).ToString()); return string.Join(" + ", parts);
    }
}

