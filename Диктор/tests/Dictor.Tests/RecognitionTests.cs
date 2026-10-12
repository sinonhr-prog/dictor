using System.Security.Cryptography;
using System.Text.Json;
using Dictor.Recognition;
using Xunit;

namespace Dictor.Tests;

public class RecognitionTests
{
    [Fact]
    public void SilenceGatePreservesQuietSignal()
    {
        Assert.True(PcmAudio.IsDigitalSilence([]));
        Assert.True(PcmAudio.IsDigitalSilence(new float[48000]));
        Assert.False(PcmAudio.IsDigitalSilence([0, 0.000001f, 0]));
    }

    [Fact]
    public void WerCountsSubstitutionDeletionAndInsertion()
    {
        Assert.Equal(2, Benchmark.Distance(["один", "два", "три"], ["один", "четыре"]));
        Assert.Equal(1, Benchmark.Distance(["один"], ["один", "два"]));
        Assert.Equal(Benchmark.Normalize("Ёж, GitHub!"), Benchmark.Normalize("еж github"));
    }

    [Fact]
    public void ModelTamperingIsRejectedBeforeNativeLoad()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var bytes = new byte[] { 1, 2, 3 };
            File.WriteAllBytes(Path.Combine(directory, "model.bin"), bytes);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(new[] { new { file = "model.bin", size = 3, sha256 = Convert.ToHexString(SHA256.HashData(bytes)) } }));
            Assert.True(File.Exists(ModelIntegrity.Verify(directory, "model.bin")));
            File.WriteAllBytes(Path.Combine(directory, "model.bin"), [1, 2, 4]);
            Assert.Throws<InvalidDataException>(() => ModelIntegrity.Verify(directory, "model.bin"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
