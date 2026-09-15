using System.Buffers.Binary;

namespace AudioEvolution.Core.Codecs;

public enum WavSampleFormat
{
    Pcm16,
    Pcm24,
    Pcm32,
    Float32
}

public sealed record WavAudioData(int SampleRate, int Channels, WavSampleFormat Format, float[] Interleaved)
{
    public int FrameCount => Channels == 0 ? 0 : Interleaved.Length / Channels;
}

/// <summary>
/// Minimal but correct RIFF/WAVE reader+writer for PCM (16/24/32-bit int) and IEEE float (32-bit)
/// data. This is the one codec the engine can rely on with zero third-party/native dependencies;
/// compressed formats (MP3/FLAC/OGG) are handled by platform-specific decoders layered on top.
/// </summary>
public static class WavFile
{
    public static WavAudioData Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        if (new string(reader.ReadChars(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        reader.ReadUInt32(); // chunk size, unused
        if (new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");

        int channels = 0, sampleRate = 0, bitsPerSample = 0;
        ushort audioFormat = 1;
        float[]? samples = null;

        while (stream.Position < stream.Length)
        {
            string chunkId = new(reader.ReadChars(4));
            uint chunkSize = reader.ReadUInt32();
            long chunkEnd = stream.Position + chunkSize;

            if (chunkId == "fmt ")
            {
                audioFormat = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = (int)reader.ReadUInt32();
                reader.ReadUInt32(); // byte rate
                reader.ReadUInt16(); // block align
                bitsPerSample = reader.ReadUInt16();
            }
            else if (chunkId == "data")
            {
                samples = DecodeDataChunk(reader, (int)chunkSize, audioFormat, bitsPerSample);
            }

            stream.Position = chunkEnd + (chunkEnd % 2); // chunks are word-aligned
        }

        if (samples is null || channels == 0 || sampleRate == 0)
            throw new InvalidDataException("WAV file missing fmt or data chunk.");

        WavSampleFormat format = (audioFormat, bitsPerSample) switch
        {
            (3, 32) => WavSampleFormat.Float32,
            (1, 16) => WavSampleFormat.Pcm16,
            (1, 24) => WavSampleFormat.Pcm24,
            (1, 32) => WavSampleFormat.Pcm32,
            _ => throw new NotSupportedException($"Unsupported WAV format {audioFormat}/{bitsPerSample}-bit.")
        };

        return new WavAudioData(sampleRate, channels, format, samples);
    }

    private static float[] DecodeDataChunk(BinaryReader reader, int byteCount, ushort audioFormat, int bitsPerSample)
    {
        int bytesPerSample = bitsPerSample / 8;
        int sampleCount = byteCount / bytesPerSample;
        var result = new float[sampleCount];
        byte[] raw = reader.ReadBytes(byteCount);

        for (int i = 0; i < sampleCount; i++)
        {
            int offset = i * bytesPerSample;
            result[i] = (audioFormat, bitsPerSample) switch
            {
                (3, 32) => BinaryPrimitives.ReadSingleLittleEndian(raw.AsSpan(offset, 4)),
                (1, 16) => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(offset, 2)) / 32768f,
                (1, 24) => Decode24BitSigned(raw.AsSpan(offset, 3)) / 8388608f,
                (1, 32) => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset, 4)) / 2147483648f,
                _ => 0f
            };
        }

        return result;
    }

    private static int Decode24BitSigned(ReadOnlySpan<byte> bytes)
    {
        int value = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
        return value;
    }

    public static void Write(Stream stream, WavAudioData data)
    {
        int bitsPerSample = data.Format switch
        {
            WavSampleFormat.Pcm16 => 16,
            WavSampleFormat.Pcm24 => 24,
            WavSampleFormat.Pcm32 => 32,
            WavSampleFormat.Float32 => 32,
            _ => throw new NotSupportedException(data.Format.ToString())
        };
        ushort audioFormat = data.Format == WavSampleFormat.Float32 ? (ushort)3 : (ushort)1;
        int bytesPerSample = bitsPerSample / 8;
        int dataBytes = data.Interleaved.Length * bytesPerSample;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF".ToCharArray());
        writer.Write((uint)(36 + dataBytes));
        writer.Write("WAVE".ToCharArray());

        writer.Write("fmt ".ToCharArray());
        writer.Write((uint)16);
        writer.Write(audioFormat);
        writer.Write((ushort)data.Channels);
        writer.Write((uint)data.SampleRate);
        writer.Write((uint)(data.SampleRate * data.Channels * bytesPerSample));
        writer.Write((ushort)(data.Channels * bytesPerSample));
        writer.Write((ushort)bitsPerSample);

        writer.Write("data".ToCharArray());
        writer.Write((uint)dataBytes);

        foreach (float sample in data.Interleaved)
        {
            switch (data.Format)
            {
                case WavSampleFormat.Float32:
                    writer.Write(sample);
                    break;
                case WavSampleFormat.Pcm16:
                    writer.Write((short)Math.Clamp(sample * 32767f, short.MinValue, short.MaxValue));
                    break;
                case WavSampleFormat.Pcm24:
                    WriteInt24(writer, (int)Math.Clamp(sample * 8388607f, -8388608f, 8388607f));
                    break;
                case WavSampleFormat.Pcm32:
                    writer.Write((int)Math.Clamp((double)sample * 2147483647.0, int.MinValue, int.MaxValue));
                    break;
            }
        }

        if (dataBytes % 2 != 0) writer.Write((byte)0); // pad byte
    }

    private static void WriteInt24(BinaryWriter writer, int value)
    {
        writer.Write((byte)(value & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
    }
}
