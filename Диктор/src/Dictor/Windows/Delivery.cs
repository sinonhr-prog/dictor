using System.Runtime.InteropServices;

namespace Dictor.Windows;

internal interface IDeliveryPort
{
    bool ModifiersDown { get; }
    uint Copy(string text);
    bool TargetUnchanged(Target? target);
    bool ClipboardUnchanged(uint sequence);
    bool SendPasteOnce();
}

internal static class Delivery
{
    public static async Task<string> Run(IDeliveryPort port, string text, Target? target, bool paste, CancellationToken token)
    {
        for (int i = 0; i < 20 && port.ModifiersDown; i++) await Task.Delay(25, token);
        uint sequence = 0;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { sequence = port.Copy(text); break; }
            catch (ExternalException) { await Task.Delay(80, token); }
        }
        if (sequence == 0) return "Буфер занят. Нажмите «Скопировать снова» в трее.";
        if (!paste) return "Текст скопирован";
        if (port.ModifiersDown || !port.TargetUnchanged(target) || !port.ClipboardUnchanged(sequence)) return "Текст скопирован. Вставьте Ctrl+V";
        return port.SendPasteOnce() ? "Команда вставки отправлена" : "Текст скопирован. Вставьте Ctrl+V";
    }
}
