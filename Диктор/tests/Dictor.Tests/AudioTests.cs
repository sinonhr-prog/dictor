using Dictor.Audio;
using Dictor.Recognition;
using Xunit;

namespace Dictor.Tests;

public class AudioTests
{
    [Fact]
    public void OverflowFailsWithoutSilentlyDroppingExistingAudio()
    {
        using var buffer = new BoundedAudioBuffer(4);
        buffer.Write([1, 2, 3], 0, 3);
        Assert.Throws<IOException>(() => buffer.Write([4, 5], 0, 2));
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());
    }
    [Fact]
    public void TruncatedWavIsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        try { File.WriteAllText(path, "RIFF0000WAVEdata9999"); Assert.Throws<InvalidDataException>(() => PcmAudio.Read(path)); }
        finally { File.Delete(path); }
    }
}
