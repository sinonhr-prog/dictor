using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Dictor.Recognition;

internal sealed class Engine : IDisposable
{
    private WhisperFactory? factory;
    private WhisperProcessor? processor;
    private WhisperVadFactory? vadFactory;
    private WhisperVadProcessor? vad;
    public void Load(string directory, bool cpu)
    {
        RuntimeOptions.RuntimeLibraryOrder = cpu ? [RuntimeLibrary.Cpu] : [RuntimeLibrary.Vulkan];
        var model = ModelIntegrity.Verify(directory, cpu ? "ggml-small-q5_1.bin" : "ggml-large-v3-turbo.bin");
        factory = WhisperFactory.FromPath(model);
        processor = factory.CreateBuilder().WithLanguage("ru").WithThreads(8).WithNoContext().Build();
        vadFactory = WhisperVadFactory.FromPath(ModelIntegrity.Verify(directory, "ggml-silero-v6.2.0.bin"));
        vad = vadFactory.CreateBuilder().WithUseGpu(false).WithThreads(2).WithSpeechPadding(TimeSpan.FromMilliseconds(250)).Build();
    }
    public async Task WarmAsync()
    {
        await foreach (var unused in processor!.ProcessAsync(new float[16000])) { }
    }
    public async Task<string> RecognizeAsync(float[] audio, CancellationToken token)
    {
        if (PcmAudio.IsDigitalSilence(audio)) return "";
        token.ThrowIfCancellationRequested();
        if (!vad!.DetectSpeech(audio).Any()) return "";
        var text = new System.Text.StringBuilder();
        await foreach (var segment in processor!.ProcessAsync(audio, token)) text.Append(segment.Text);
        return text.ToString().Trim();
    }
    public void Dispose() { processor?.Dispose(); factory?.Dispose(); vad?.Dispose(); vadFactory?.Dispose(); }
}
