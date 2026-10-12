using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Whisper.net;

namespace Dictor.Recognition;

internal static class Benchmark
{
    public static async Task Run(WhisperProcessor processor, string directory)
    {
        var phrases = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(directory, "phrases.json")))!;
        var rows = new List<object>();
        for (int i = 0; i < phrases.Length; i++)
        {
            var audio = PcmAudio.Read(Path.Combine(directory, $"{i:D2}.wav"));
            var timer = Stopwatch.StartNew();
            string result = "";
            if (!PcmAudio.IsDigitalSilence(audio))
                await foreach (var segment in processor.ProcessAsync(audio)) result += segment.Text;
            var elapsed = timer.Elapsed.TotalMilliseconds;
            var reference = Normalize(phrases[i]);
            var actual = Normalize(result);
            rows.Add(new { id = i, seconds = audio.Length / 16000d, asrMs = elapsed, edits = Distance(reference, actual), words = reference.Length });
        }
        Console.WriteLine(JsonSerializer.Serialize(new { synthetic = true, rows, peakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 }));
    }

    internal static string[] Normalize(string text) => Regex.Replace(text.ToLowerInvariant().Replace('ё', 'е'), @"[^\p{L}\p{Nd}]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    internal static int Distance(string[] expected, string[] actual)
    {
        var previous = Enumerable.Range(0, actual.Length + 1).ToArray();
        for (int i = 1; i <= expected.Length; i++)
        {
            var current = new int[actual.Length + 1]; current[0] = i;
            for (int j = 1; j <= actual.Length; j++)
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + (expected[i - 1] == actual[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[^1];
    }
}
