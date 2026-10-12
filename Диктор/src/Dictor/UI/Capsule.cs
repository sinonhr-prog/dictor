using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Dictor.Settings;
using Dictor.Windows;

namespace Dictor.UI;

internal sealed class Capsule : Window
{
    private readonly TextBlock label = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxHeight = 44 };
    private readonly TextBlock timer = new() { FontSize = 12, FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0), MinWidth = 36 };
    private readonly StackPanel wave = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Height = 30 };
    private readonly Ellipse dot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
    private readonly Button stop = new() { Width = 36, Height = 36, Padding = new Thickness(0), BorderThickness = new Thickness(0), ToolTip = "Остановить запись", Focusable = false };
    private readonly Rectangle[] bars = new Rectangle[14];
    private bool wasRecording;
    public event Action? Stop;
    public Capsule()
    {
        Width = 300; Height = 64; ShowActivated = false; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        Design.Apply(this, "System");
        var shell = new Border { Width = 300, Height = 64, CornerRadius = new CornerRadius(32), BorderThickness = new Thickness(1), Padding = new Thickness(14, 10, 12, 10) };
        shell.SetResourceReference(Border.BackgroundProperty, "Surface"); shell.SetResourceReference(Border.BorderBrushProperty, "Line");
        Content = new Viewbox { Child = shell, Stretch = Stretch.Uniform };
        var grid = new Grid(); shell.Child = grid;
        foreach (var width in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) grid.ColumnDefinitions.Add(new() { Width = width });
        grid.Children.Add(dot);
        var center = new Grid(); center.Children.Add(wave); center.Children.Add(label); Grid.SetColumn(center, 1); grid.Children.Add(center);
        for (int i = 0; i < bars.Length; i++) { bars[i] = new Rectangle { Width = 3, Height = 3, RadiusX = 1.5, RadiusY = 1.5, Margin = new Thickness(1.5, 0, 1.5, 0), VerticalAlignment = VerticalAlignment.Center }; bars[i].SetResourceReference(Shape.FillProperty, "Accent"); wave.Children.Add(bars[i]); }
        Grid.SetColumn(timer, 2); grid.Children.Add(timer); Grid.SetColumn(stop, 3); grid.Children.Add(stop);
        stop.Style = (Style)FindResource("RoundStop");
        var square = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(2) }; square.SetResourceReference(Border.BackgroundProperty, "Accent"); stop.Content = square;
        System.Windows.Automation.AutomationProperties.SetName(stop, "Остановить запись");
        stop.Click += (_, _) => Stop?.Invoke();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            Desktop.SetWindowLongPtr(handle, -20, Desktop.GetWindowLongPtr(handle, -20) | 0x08000080);
            HwndSource.FromHwnd(handle).AddHook((nint w, int m, nint p, nint l, ref bool handled) => { if (m == 0x21) { handled = true; return 3; } return 0; });
        };
    }
    public void Configure(Preferences settings)
    {
        Design.Apply(this, settings.Theme); Width = 300 * settings.Scale; Height = 64 * settings.Scale;
        var screens = System.Windows.Forms.Screen.AllScreens;
        var area = screens[Math.Clamp(settings.Monitor, 0, screens.Length - 1)].WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var point = transform.Transform(new System.Windows.Point(area.Left + area.Width / 2d, settings.Position == "Top" ? area.Top + 24 : area.Bottom - 24));
        Left = point.X - Width / 2; Top = settings.Position == "Top" ? point.Y : point.Y - Height;
    }
    public void Update(string text, float peak = 0, bool recording = false)
    {
        ToolTip = text; System.Windows.Automation.AutomationProperties.SetName(this, text);
        wave.Visibility = timer.Visibility = stop.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        label.Visibility = recording ? Visibility.Collapsed : Visibility.Visible;
        dot.SetResourceReference(Shape.FillProperty, recording ? "Recording" : "Accent");
        label.Text = text;
        if (recording)
        {
            if (!wasRecording) foreach (var bar in bars) bar.Height = 3;
            for (int i = 0; i < bars.Length - 1; i++) bars[i].Height = bars[i + 1].Height;
            bars[^1].Height = 3 + 27 * Math.Sqrt(Math.Clamp(float.IsFinite(peak) ? peak : 0, 0, 1));
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\d{2}:\d{2}");
            timer.Text = match.Success ? match.Value : "00:00";
        }
        wasRecording = recording;
    }
}
