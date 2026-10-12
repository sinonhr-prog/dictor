using System.Diagnostics;
using System.Text.Json;
using Dictor.Recognition;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Dictor;

internal static class Program
{
    [STAThread] public static int Main(string[] args) { if (args is ["--shutdown-check"]) return UI.DictorApp.RunApp(shutdownCheck: true); if (args is ["--design-preview", var output]) return UI.DesignPreview.Render(output); if (args is ["--microphone-acceptance"]) return UI.DictorApp.RunApp(true); if (args is ["--engine-check", var models, var samples]) return EngineCheck.Run(models, samples).GetAwaiter().GetResult(); if (args.Length > 0) return Probe(args).GetAwaiter().GetResult(); return UI.DictorApp.RunApp(); }
    private static async Task<int> Probe(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("Usage: Dictor <models-dir> <model-file> <cpu|vulkan> [authorized.wav]"); return 2; }
        try
        {
            RuntimeOptions.RuntimeLibraryOrder = args[2] switch { "cpu" => [RuntimeLibrary.Cpu], "vulkan" => [RuntimeLibrary.Vulkan], _ => throw new ArgumentException("BACKEND_INVALID") };
            var path = ModelIntegrity.Verify(Path.GetFullPath(args[0]), args[1]);
            var timer = Stopwatch.StartNew();
            using var factory = WhisperFactory.FromPath(path);
            using var processor = factory.CreateBuilder().WithLanguage("ru").WithThreads(8).Build();
            var loadMs = timer.Elapsed.TotalMilliseconds;
            if (args.Length > 3 && Directory.Exists(args[3]))
            {
                await Benchmark.Run(processor, args[3]);
                return 0;
            }
            timer.Restart();
            int segments = 0, characters = 0;
            if (args.Length > 3)
            {
                var audio = PcmAudio.Read(args[3]);
                if (!PcmAudio.IsDigitalSilence(audio))
                    await foreach (var segment in processor.ProcessAsync(audio)) { segments++; characters += segment.Text.Length; }
            }
            else
            {
                var silence = new float[48000];
                if (!PcmAudio.IsDigitalSilence(silence))
                    await foreach (var segment in processor.ProcessAsync(silence)) { segments++; characters += segment.Text.Length; }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { backend = args[2], loadMs, asrMs = timer.Elapsed.TotalMilliseconds, segments, characters, peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 }));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { code = "ASR_PROBE_FAILED", type = error.GetType().Name }));
            return 1;
        }

    }
}



