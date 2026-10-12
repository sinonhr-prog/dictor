using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Dictor.Audio;

internal sealed class Capture : IDisposable
{
    private readonly MMDevice device;
    private readonly WasapiCapture capture;
    private readonly BoundedAudioBuffer buffer;
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool requestedStop;
    private bool disposed;
    private Exception? failure;
    public float Level { get; private set; }
    public event Action? LimitReached;

    public Capture(string? endpoint)
    {
        using var enumerator = new MMDeviceEnumerator();
        device = endpoint is null ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications) : enumerator.GetDevice(endpoint);
        try { capture = new WasapiCapture(device); }
        catch { device.Dispose(); throw; }
        buffer = new BoundedAudioBuffer(capture.WaveFormat.AverageBytesPerSecond * 600L);
        capture.DataAvailable += (_, e) =>
        {
            if (failure is not null) return;
            try { buffer.Write(e.Buffer, 0, e.BytesRecorded); }
            catch (IOException error) { failure = error; LimitReached?.Invoke(); return; }
            // WASAPI shared mode uses IEEE float; PCM formats are handled by NAudio during conversion.
            if (capture.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat || capture.WaveFormat is WaveFormatExtensible ext && ext.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"))
            {
                float peak = 0;
                for (int i = 0; i + 4 <= e.BytesRecorded; i += 4) peak = Math.Max(peak, Math.Abs(BitConverter.ToSingle(e.Buffer, i)));
                Level = peak;
            }
        };
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) stopped.TrySetException(new IOException("CAPTURE_DEVICE_LOST"));
            else stopped.TrySetResult();
            if (!requestedStop) LimitReached?.Invoke();
        };
    }

    public void Start() => capture.StartRecording();
    public async Task<float[]> StopAsync()
    {
        requestedStop = true;
        capture.StopRecording();
        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (failure is not null) throw failure;
        var format = capture.WaveFormat;
        capture.Dispose();
        device.Dispose();
        return await Task.Run(() => ConvertAudio(format));
    }

    private float[] ConvertAudio(WaveFormat format)
    {
        buffer.Position = 0;
        using var source = new RawSourceWaveStream(buffer, format);
        ISampleProvider samples = source.ToSampleProvider();
        if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples);
        if (samples.WaveFormat.Channels != 1) throw new InvalidOperationException("CHANNELS_UNSUPPORTED");
        if (samples.WaveFormat.SampleRate != 16000) samples = new WdlResamplingSampleProvider(samples, 16000);
        var result = new List<float>();
        var chunk = new float[16000];
        int read;
        while ((read = samples.Read(chunk, 0, chunk.Length)) > 0) result.AddRange(chunk.AsSpan(0, read).ToArray());
        return result.ToArray();
    }

    public void Dispose() { if (disposed) return; disposed = true; capture.Dispose(); device.Dispose(); buffer.Dispose(); }
}
