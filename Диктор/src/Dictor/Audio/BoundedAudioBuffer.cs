namespace Dictor.Audio;

internal sealed class BoundedAudioBuffer(long capacity) : MemoryStream
{
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Length + count > capacity) throw new IOException("AUDIO_BUFFER_LIMIT");
        base.Write(buffer, offset, count);
    }
}
