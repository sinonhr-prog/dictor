namespace Dictor.Recognition;

internal static class PcmAudio
{
    public static float[] Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("WAV_RIFF");
        reader.ReadInt32();
        if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("WAV_FORMAT");
        bool valid = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            var id = new string(reader.ReadChars(4));
            var size = reader.ReadUInt32();
            var end = reader.BaseStream.Position + size;
            if (end > reader.BaseStream.Length) throw new InvalidDataException("WAV_TRUNCATED");
            if (id == "fmt ")
            {
                if (size < 16) throw new InvalidDataException("WAV_FORMAT");
                var encoding = reader.ReadInt16();
                var channels = reader.ReadInt16();
                var rate = reader.ReadInt32();
                reader.ReadInt32(); reader.ReadInt16();
                var bits = reader.ReadInt16();
                valid = encoding == 1 && channels == 1 && rate == 16000 && bits == 16;
            }
            else if (id == "data")
            {
                if (!valid || size % 2 != 0 || size > 16000 * 2 * 600) throw new InvalidDataException("WAV_UNSUPPORTED");
                var samples = new float[size / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                return samples;
            }
            reader.BaseStream.Position = end + (size % 2);
        }
        throw new InvalidDataException("WAV_DATA_MISSING");
    }

    // Only digital silence. No arbitrary amplitude gate that could discard quiet speech.
    public static bool IsDigitalSilence(float[] samples) => samples.All(x => x == 0);
}
