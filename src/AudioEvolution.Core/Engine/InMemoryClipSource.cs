namespace AudioEvolution.Core.Engine;

/// <summary>
/// Simple IAudioClipSource backed by an in-memory interleaved float buffer. Used for unit
/// tests and for small clips (e.g. one-shots) that fit comfortably in memory; long recordings
/// use a streaming disk-backed implementation instead (not needed for the engine's core mixing
/// correctness, which is what this type exists to make testable).
/// </summary>
public sealed class InMemoryClipSource : IAudioClipSource
{
    private readonly float[] _interleaved;

    public InMemoryClipSource(int sampleRate, int channels, float[] interleaved)
    {
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
        if (interleaved.Length % channels != 0)
            throw new ArgumentException("Buffer length must be a multiple of channel count.", nameof(interleaved));

        SampleRate = sampleRate;
        Channels = channels;
        _interleaved = interleaved;
        TotalFrames = interleaved.Length / channels;
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public long TotalFrames { get; }

    public int ReadInterleaved(long startFrame, int frameCount, Span<float> destination)
    {
        destination.Clear();
        if (startFrame >= TotalFrames || frameCount <= 0) return 0;

        long framesAvailable = Math.Min(frameCount, TotalFrames - startFrame);
        int srcOffset = (int)(startFrame * Channels);
        int copyLength = (int)(framesAvailable * Channels);

        _interleaved.AsSpan(srcOffset, copyLength).CopyTo(destination);
        return (int)framesAvailable;
    }
}
