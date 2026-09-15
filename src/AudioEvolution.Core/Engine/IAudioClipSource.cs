namespace AudioEvolution.Core.Engine;

/// <summary>
/// Provides decoded float32 samples for a clip's source file, independent of the original
/// codec. Implementations cache/stream from disk; the mixer only ever sees interleaved floats.
/// </summary>
public interface IAudioClipSource
{
    int SampleRate { get; }
    int Channels { get; }
    long TotalFrames { get; }

    /// <summary>
    /// Reads <paramref name="frameCount"/> frames starting at <paramref name="startFrame"/> into
    /// <paramref name="destination"/> (interleaved, length &gt;= frameCount * Channels).
    /// Frames beyond the end of the source are filled with silence. Returns frames actually
    /// containing source audio (the rest of destination is guaranteed zeroed).
    /// </summary>
    int ReadInterleaved(long startFrame, int frameCount, Span<float> destination);
}
