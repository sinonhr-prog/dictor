using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dictor.Settings;

namespace Dictor.UI;

internal static class DesignPreview
{
    public static int Render(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new Application();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            var settings = new SettingsWindow(new Preferences { Theme = theme }, _ => true, _ => { }, () => false, true);
            Save((FrameworkElement)settings.Content, 640, Path.Combine(directory, $"settings-{theme}.png"));
            foreach (var expander in Descendants<Expander>((DependencyObject)settings.Content)) expander.IsExpanded = true;
            Save((FrameworkElement)settings.Content, 640, Path.Combine(directory, $"settings-{theme}-expanded.png"));
            settings.Close();
            var capsule = new Capsule(); capsule.Configure(new Preferences { Theme = theme });
            foreach (var level in new[] { .04f, .15f, .4f, .12f, .8f, .3f, .06f, .6f, .2f, 1f, .12f, .4f, .04f, .2f }) capsule.Update("Запись · 00:12", level, true);
            Save((FrameworkElement)capsule.Content, 300, Path.Combine(directory, $"capsule-{theme}.png"));
            capsule.Update("Текст скопирован. Вставьте Ctrl+V");
            Save((FrameworkElement)capsule.Content, 300, Path.Combine(directory, $"capsule-{theme}-done.png"));
            capsule.Close();
        }
        app.Shutdown(); return 0;
    }
    private static void Save(FrameworkElement view, double width, string path)
    {
        view.Measure(new Size(width, double.PositiveInfinity)); view.Arrange(new Rect(0, 0, width, view.DesiredSize.Height)); view.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(width * 1.5), (int)Math.Ceiling(view.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        image.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(path); encoder.Save(stream);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
}

