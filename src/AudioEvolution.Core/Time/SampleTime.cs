using System.Text.Json.Serialization;

namespace AudioEvolution.Core.Time;

/// <summary>
/// Sample-accurate position in an audio timeline. All engine-internal timing uses this
/// instead of TimeSpan/double seconds, since float seconds accumulate rounding error
/// across long sessions and break sample-accurate editing/looping.
/// </summary>
public readonly record struct SampleTime : IComparable<SampleTime>
{
    public long Samples { get; }
    public int SampleRate { get; }

    /// <summary>
    /// [JsonConstructor] is required: System.Text.Json defaults to a struct's implicit
    /// parameterless constructor and this type has no property setters, so without it every
    /// SampleTime silently deserializes to (0, 0) instead of its saved value.
    /// </summary>
    [JsonConstructor]
    public SampleTime(long samples, int sampleRate)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        Samples = samples;
        SampleRate = sampleRate;
    }

    public static SampleTime Zero(int sampleRate) => new(0, sampleRate);

    public static SampleTime FromSeconds(double seconds, int sampleRate) =>
        new((long)Math.Round(seconds * sampleRate, MidpointRounding.AwayFromZero), sampleRate);

    /// <summary>
    /// Zero for the struct's default value (SampleRate 0, e.g. an unset AudioClip.FadeInLength) —
    /// a zero-sample duration is zero seconds regardless of rate, so this avoids a 0/0 NaN
    /// that would otherwise poison any code (including JSON serialization) touching this property.
    /// </summary>
    [JsonIgnore]
    public double TotalSeconds => SampleRate == 0 ? 0 : (double)Samples / SampleRate;

    public SampleTime ConvertTo(int targetSampleRate)
    {
        if (targetSampleRate == SampleRate) return this;
        double ratio = (double)targetSampleRate / SampleRate;
        return new SampleTime((long)Math.Round(Samples * ratio, MidpointRounding.AwayFromZero), targetSampleRate);
    }

    private void EnsureSameRate(SampleTime other)
    {
        if (other.SampleRate != SampleRate)
            throw new InvalidOperationException(
                $"Cannot combine SampleTime values at different sample rates ({SampleRate} vs {other.SampleRate}) without an explicit ConvertTo.");
    }

    public static SampleTime operator +(SampleTime a, SampleTime b)
    {
        a.EnsureSameRate(b);
        return new SampleTime(a.Samples + b.Samples, a.SampleRate);
    }

    public static SampleTime operator -(SampleTime a, SampleTime b)
    {
        a.EnsureSameRate(b);
        return new SampleTime(a.Samples - b.Samples, a.SampleRate);
    }

    public int CompareTo(SampleTime other)
    {
        EnsureSameRate(other);
        return Samples.CompareTo(other.Samples);
    }

    public static bool operator <(SampleTime a, SampleTime b) => a.CompareTo(b) < 0;
    public static bool operator >(SampleTime a, SampleTime b) => a.CompareTo(b) > 0;
    public static bool operator <=(SampleTime a, SampleTime b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SampleTime a, SampleTime b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Samples}@{SampleRate}Hz ({TotalSeconds:F3}s)";
}
