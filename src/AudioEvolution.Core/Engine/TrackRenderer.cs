using System.Buffers;
using AudioEvolution.Core.Model;
using AudioEvolution.Core.Time;

namespace AudioEvolution.Core.Engine;

/// <summary>
/// Renders a single track's clips into a stereo interleaved buffer for a given range of the
/// master timeline. Overlapping clips on the same track sum together (matches AEM behavior on
/// lanes that permit overlap); each clip contributes its own gain/fade envelope via
/// AudioClip.GainAt, and track volume/pan (static or automated) is applied here — this is the
/// only place with per-sample resolution, so automation has to be resolved at this level rather
/// than as a flat per-block multiply in MixEngine.
/// </summary>
public sealed class TrackRenderer
{
    private readonly Func<string, IAudioClipSource> _sourceResolver;

    public TrackRenderer(Func<string, IAudioClipSource> sourceResolver)
    {
        _sourceResolver = sourceResolver;
    }

    /// <summary>
    /// Renders [startFrame, startFrame+frameCount) of the track's timeline into
    /// destination (interleaved stereo: length == frameCount * 2), summing overlapping clips
    /// and applying the track's volume/pan — sample-accurately if either is automated.
    /// </summary>
    public void Render(Track track, long startFrame, int frameCount, int sampleRate, Span<float> destination)
    {
        destination.Clear();
        if (track.Muted) return;

        Span<float> monoBuf = frameCount <= 4096
            ? stackalloc float[frameCount]
            : new float[frameCount];

        foreach (var clip in track.Clips)
        {
            long clipStart = clip.TimelinePosition.Samples;
            long clipEnd = clip.TimelineEnd.Samples;
            long rangeEnd = startFrame + frameCount;

            if (clipEnd <= startFrame || clipStart >= rangeEnd) continue; // no overlap

            var source = _sourceResolver(clip.SourceFilePath);

            long overlapStart = Math.Max(startFrame, clipStart);
            long overlapEnd = Math.Min(rangeEnd, clipEnd);
            int destOffset = (int)(overlapStart - startFrame);
            int overlapFrames = (int)(overlapEnd - overlapStart);

            long sourceReadStart = clip.SourceStart.Samples + (overlapStart - clipStart);

            float[] rented = ArrayPool<float>.Shared.Rent(overlapFrames * source.Channels);
            try
            {
                Span<float> srcInterleaved = rented.AsSpan(0, overlapFrames * source.Channels);
                source.ReadInterleaved(sourceReadStart, overlapFrames, srcInterleaved);

                for (int i = 0; i < overlapFrames; i++)
                {
                    float sample = source.Channels == 1
                        ? srcInterleaved[i]
                        : (srcInterleaved[i * source.Channels] + srcInterleaved[i * source.Channels + 1]) * 0.5f;

                    long offsetIntoClip = overlapStart - clipStart + i;
                    float gain = clip.GainAt(offsetIntoClip);
                    monoBuf[destOffset + i] += sample * gain;
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(rented);
            }
        }

        var volumeLane = FindLane(track, AutomationTarget.Volume);
        var panLane = FindLane(track, AutomationTarget.Pan);

        if (volumeLane is null && panLane is null)
        {
            // Fast path: static gain, computed once instead of per sample.
            float volume = track.VolumeLinear;
            var (left, right) = track.PanGains();
            for (int i = 0; i < frameCount; i++)
            {
                destination[i * 2] += monoBuf[i] * volume * left;
                destination[i * 2 + 1] += monoBuf[i] * volume * right;
            }
            return;
        }

        for (int i = 0; i < frameCount; i++)
        {
            var sampleTime = new SampleTime(startFrame + i, sampleRate);
            float volumeDb = volumeLane?.ValueAt(sampleTime, track.VolumeDb) ?? track.VolumeDb;
            float pan = panLane?.ValueAt(sampleTime, track.Pan) ?? track.Pan;

            float volume = (float)Math.Pow(10, volumeDb / 20.0);
            double angle = (Math.Clamp(pan, -1f, 1f) + 1.0) * Math.PI / 4.0;
            float left = (float)Math.Cos(angle);
            float right = (float)Math.Sin(angle);

            destination[i * 2] += monoBuf[i] * volume * left;
            destination[i * 2 + 1] += monoBuf[i] * volume * right;
        }
    }

    private static AutomationLane? FindLane(Track track, AutomationTarget target)
    {
        foreach (var lane in track.AutomationLanes)
        {
            if (lane.Target == target && lane.Enabled && lane.Points.Count > 0)
                return lane;
        }
        return null;
    }
}
