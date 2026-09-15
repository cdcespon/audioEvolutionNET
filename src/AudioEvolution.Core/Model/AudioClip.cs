using AudioEvolution.Core.Time;

namespace AudioEvolution.Core.Model;

/// <summary>
/// A non-destructive reference to a region of a source audio file placed on a track.
/// Trimming/moving a clip never touches the underlying file (SourceFilePath) — only
/// SourceStart/SourceLength/TimelinePosition change, matching AEM's non-destructive editing model.
/// </summary>
public sealed class AudioClip
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string SourceFilePath { get; set; }

    /// <summary>Offset into the source file where this clip's audio starts.</summary>
    public required SampleTime SourceStart { get; set; }

    /// <summary>Duration of audio taken from the source file.</summary>
    public required SampleTime SourceLength { get; set; }

    /// <summary>Position of this clip on the track timeline.</summary>
    public required SampleTime TimelinePosition { get; set; }

    public float GainDb { get; set; } = 0f;
    public bool Muted { get; set; }
    public bool Reversed { get; set; }

    // Plain sample counts, not SampleTime: they're always relative to this clip's own source
    // sample rate, and SampleTime's default value (an unset FadeInLength/FadeOutLength) carries
    // an invalid zero sample rate that both breaks TotalSeconds (0/0) and fails strict
    // JSON round-tripping through SampleTime's validating constructor.
    public long FadeInSamples { get; set; }
    public long FadeOutSamples { get; set; }
    public FadeCurve FadeInCurve { get; set; } = FadeCurve.Linear;
    public FadeCurve FadeOutCurve { get; set; } = FadeCurve.Linear;

    [System.Text.Json.Serialization.JsonIgnore]
    public SampleTime TimelineEnd => TimelinePosition + SourceLength;

    [System.Text.Json.Serialization.JsonIgnore]
    public float GainLinear => (float)Math.Pow(10, GainDb / 20.0);

    /// <summary>
    /// Combined linear gain at a given position within the clip (0-based sample offset from
    /// TimelinePosition), including clip gain and fade envelope. Does not include track/bus gain.
    /// </summary>
    public float GainAt(long offsetIntoClip)
    {
        if (Muted) return 0f;

        float gain = GainLinear;

        if (FadeInSamples > 0 && offsetIntoClip < FadeInSamples)
        {
            double t = (double)offsetIntoClip / FadeInSamples;
            gain *= FadeCurveEvaluator.EvaluateFadeIn(FadeInCurve, t);
        }

        long fadeOutStart = SourceLength.Samples - FadeOutSamples;
        if (FadeOutSamples > 0 && offsetIntoClip >= fadeOutStart)
        {
            double t = (double)(offsetIntoClip - fadeOutStart) / FadeOutSamples;
            gain *= FadeCurveEvaluator.EvaluateFadeOut(FadeOutCurve, t);
        }

        return gain;
    }
}
