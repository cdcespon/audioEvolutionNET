using AudioEvolution.Core.Time;

namespace AudioEvolution.Core.Model;

public enum AutomationTarget
{
    Volume,
    Pan,
    Mute,
    SendLevel,
    PluginParameter
}

public readonly record struct AutomationPoint(SampleTime Time, float Value);

/// <summary>
/// A single automated parameter curve (e.g. track volume over time). Points must stay
/// sorted by Time; callers add via AddPoint rather than mutating the list directly.
/// </summary>
public sealed class AutomationLane
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required AutomationTarget Target { get; set; }
    public string? PluginParameterId { get; set; }
    public bool Enabled { get; set; } = true;

    // `init`, not a bare `get`: System.Text.Json's reflection deserializer does not populate
    // a get-only collection property, it silently leaves it empty on load.
    public List<AutomationPoint> Points { get; init; } = new();

    public void AddPoint(AutomationPoint point)
    {
        int index = Points.FindIndex(p => p.Time > point.Time);
        if (index < 0) Points.Add(point);
        else Points.Insert(index, point);
    }

    public void RemovePointAt(SampleTime time) => Points.RemoveAll(p => p.Time == time);

    /// <summary>Linearly interpolated value at an arbitrary point in time.</summary>
    public float ValueAt(SampleTime time, float defaultValue)
    {
        if (Points.Count == 0) return defaultValue;
        if (time <= Points[0].Time) return Points[0].Value;
        if (time >= Points[^1].Time) return Points[^1].Value;

        for (int i = 0; i < Points.Count - 1; i++)
        {
            var a = Points[i];
            var b = Points[i + 1];
            if (time >= a.Time && time <= b.Time)
            {
                long span = b.Time.Samples - a.Time.Samples;
                if (span == 0) return a.Value;
                double t = (double)(time.Samples - a.Time.Samples) / span;
                return (float)(a.Value + (b.Value - a.Value) * t);
            }
        }

        return defaultValue;
    }
}
