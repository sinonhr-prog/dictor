using System.Runtime.InteropServices;
using Dictor.Windows;
using Xunit;

namespace Dictor.Tests;

public class DeliveryTests
{
    private sealed class Port : IDeliveryPort
    {
        public bool ModifiersDown => false;
        public int Busy, Copies, Pastes;
        public bool TargetValid = true, ClipboardValid = true, PasteSuccess = true;
        public uint Copy(string text) { Copies++; if (Busy-- > 0) throw new ExternalException(); return 1; }
        public bool TargetUnchanged(Target? target) => TargetValid;
        public bool ClipboardUnchanged(uint sequence) => ClipboardValid;
        public bool SendPasteOnce() { Pastes++; return PasteSuccess; }
    }
    [Fact]
    public async Task BusyClipboardHasBoundedRetries()
    {
        var port = new Port { Busy = 10 };
        await Delivery.Run(port, "test", null, true, CancellationToken.None);
        Assert.Equal(5, port.Copies); Assert.Equal(0, port.Pastes);
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ChangedTargetOrClipboardPreventsPaste(bool target, bool clipboard)
    {
        var port = new Port { TargetValid = target, ClipboardValid = clipboard };
        await Delivery.Run(port, "test", null, true, CancellationToken.None);
        Assert.Equal(1, port.Copies); Assert.Equal(0, port.Pastes);
    }
    [Fact]
    public async Task UncertainPasteIsNeverRepeated()
    {
        var port = new Port { PasteSuccess = false };
        await Delivery.Run(port, "test", null, true, CancellationToken.None);
        Assert.Equal(1, port.Pastes);
    }
    [Fact]
    public async Task CancellationDoesNotWriteClipboard()
    {
        var port = new Port();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Delivery.Run(port, "test", null, true, new CancellationToken(true)));
        Assert.Equal(0, port.Copies); Assert.Equal(0, port.Pastes);
    }
}
