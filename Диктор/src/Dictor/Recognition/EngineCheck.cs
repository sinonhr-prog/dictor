using System.Diagnostics;
using System.Text.Json;

namespace Dictor.Recognition;

internal static class EngineCheck
{
    public static async Task<int> Run(string models, string samples)
    {
        using var engine = new Engine();
        var timer = Stopwatch.StartNew();
        engine.Load(models, false); await engine.WarmAsync();
        var startupMs = timer.Elapsed.TotalMilliseconds;
        var phrases = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(samples, "phrases.json")))!;
        var rows = new List<object>();
        for (int i = 0; i < phrases.Length; i++)
        {
            var audio = PcmAudio.Read(Path.Combine(samples, $"{i:D2}.wav"));
            timer.Restart();
            var text = await engine.RecognizeAsync(audio, CancellationToken.None);
            var expected = Benchmark.Normalize(phrases[i]);
            rows.Add(new { id = i, asrMs = timer.Elapsed.TotalMilliseconds, edits = Benchmark.Distance(expected, Benchmark.Normalize(text)), words = expected.Length, detected = text.Length > 0 });
        }
        var random = new Random(17);
        var noise = Enumerable.Range(0, 48000).Select(_ => (float)(random.NextDouble() - .5) * .02f).ToArray();
        bool silencePass = (await engine.RecognizeAsync(new float[48000], CancellationToken.None)).Length == 0;
        bool noisePass = (await engine.RecognizeAsync(noise, CancellationToken.None)).Length == 0;
        var first = PcmAudio.Read(Path.Combine(samples, "00.wav"));
        var quiet = first.Select(x => x * .05f).ToArray();
        bool quietPass = (await engine.RecognizeAsync(quiet, CancellationToken.None)).Length > 0;
        var timings = new List<object>();
        foreach (int seconds in new[] { 10, 60 })
        {
            var audio = new float[16000 * seconds];
            for (int offset = 0; offset + first.Length <= audio.Length; offset += first.Length + 4000) Array.Copy(first, 0, audio, offset, first.Length);
            var values = new List<double>();
            for (int run = 0; run < 30; run++)
            {
                timer.Restart(); await engine.RecognizeAsync(audio, CancellationToken.None); values.Add(timer.Elapsed.TotalMilliseconds);
            }
            values.Sort();
            timings.Add(new { seconds, runs = values.Count, p50Ms = values[14], p95Ms = values[28], rtf95 = values[28] / (seconds * 1000) });
        }
        Console.WriteLine(JsonSerializer.Serialize(new { startupMs, rows, silencePass, noisePass, quietPass, timings, peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 }));
        return silencePass && noisePass && quietPass ? 0 : 1;
    }
}
