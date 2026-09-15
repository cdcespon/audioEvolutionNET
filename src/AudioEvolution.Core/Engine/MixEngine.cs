using AudioEvolution.Core.Model;

namespace AudioEvolution.Core.Engine;

/// <summary>
/// Sums all tracks of a project into a master stereo buffer, applying per-track volume/pan
/// and standard solo semantics (any soloed track silences all non-soloed tracks). This is the
/// pure-math mixing core; the realtime audio backend (WASAPI/ASIO, Windows-only) pulls buffers
/// from here on the audio callback thread instead of doing any of this logic itself.
/// </summary>
public sealed class MixEngine
{
    private readonly TrackRenderer _renderer;

    public MixEngine(Func<string, IAudioClipSource> sourceResolver)
    {
        _renderer = new TrackRenderer(sourceResolver);
    }

    /// <summary>Renders the full mix for [startFrame, startFrame+frameCount) into destination (interleaved stereo).</summary>
    public void RenderMix(Project project, long startFrame, int frameCount, Span<float> destination)
    {
        destination.Clear();
        bool anySoloed = project.Tracks.Any(t => t.Soloed && t.Type != TrackType.Master);

        Span<float> trackBuf = frameCount * 2 <= 8192
            ? stackalloc float[frameCount * 2]
            : new float[frameCount * 2];

        foreach (var track in project.Tracks)
        {
            if (track.Type == TrackType.Master) continue;
            if (anySoloed && !track.Soloed) continue;

            trackBuf.Clear();
            // TrackRenderer already applies the track's volume/pan (static or automated,
            // sample-accurately) — do not re-apply volume here or it gets applied twice.
            _renderer.Render(track, startFrame, frameCount, project.SampleRate, trackBuf);

            for (int i = 0; i < trackBuf.Length; i++)
                destination[i] += trackBuf[i];
        }
    }
}
