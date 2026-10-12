using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Dictor.Windows;

internal sealed record Target(nint Window, int ProcessId, long Started, int[] Element);
internal static class Desktop
{
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out int process);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public Keyboard Keyboard;
        [FieldOffset(0)] public Mouse Mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }

    public static Target? Snapshot()
    {
        try
        {
            var window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out var pid);
            using var process = Process.GetProcessById(pid);
            var element = AutomationElement.FocusedElement;
            if (element is null || element.Current.IsPassword || element.Current.ProcessId != pid || !element.Current.IsEnabled) return null;
            var id = element.GetRuntimeId();
            if (id.Length == 0) return null;
            return new Target(window, pid, process.StartTime.ToUniversalTime().Ticks, id);
        }
        catch { return null; }
    }

    public static bool Matches(Target? original, Target? current) => original is not null && current is not null &&
        original.Window == current.Window && original.ProcessId == current.ProcessId && original.Started == current.Started && original.Element.SequenceEqual(current.Element);

    public static Task<string> DeliverAsync(string text, Target? target, bool paste, CancellationToken token) => Delivery.Run(new Port(), text, target, paste, token);
    private sealed class Port : IDeliveryPort
    {
        public bool ModifiersDown => Desktop.ModifiersDown();
        public uint Copy(string text) { System.Windows.Clipboard.SetText(text); return GetClipboardSequenceNumber(); }
        public bool TargetUnchanged(Target? target) => Matches(target, Snapshot());
        public bool ClipboardUnchanged(uint sequence) => sequence == GetClipboardSequenceNumber();
        public bool SendPasteOnce()
        {
            var inputs = new[] { Key(0x11, false), Key(0x56, false), Key(0x56, true), Key(0x11, true) };
            if (SendInput(4, inputs, Marshal.SizeOf<Input>()) == 4) return true;
            SendInput(2, [Key(0x56, true), Key(0x11, true)], Marshal.SizeOf<Input>());
            return false;
        }
    }
    private static bool ModifiersDown() => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    private static Input Key(ushort key, bool up) => new() { Type = 1, Data = new InputUnion { Keyboard = new Keyboard { Key = key, Flags = up ? 2u : 0u } } };
}

