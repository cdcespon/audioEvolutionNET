namespace AudioEvolution.Core.Model;

public enum TrackType
{
    Audio,
    Midi,
    Bus,
    Master
}

public sealed class Track
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required TrackType Type { get; set; }

    public float VolumeDb { get; set; } = 0f;
    public float Pan { get; set; } = 0f; // -1 (full left) .. +1 (full right)
    public bool Muted { get; set; }
    public bool Soloed { get; set; }
    public bool RecordArmed { get; set; }

    /// <summary>Target bus/master this track's output is routed to. Null = master.</summary>
    public Guid? OutputBusId { get; set; }

    // `init` rather than a bare `get` is required for these: System.Text.Json's reflection-based
    // deserializer does not populate a get-only collection property, it silently leaves it empty.
    public List<AudioClip> Clips { get; init; } = new();
    public List<AutomationLane> AutomationLanes { get; init; } = new();
    public List<EffectInstance> Effects { get; init; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public float VolumeLinear => (float)Math.Pow(10, VolumeDb / 20.0);

    /// <summary>Equal-power pan gains for (left, right) given Pan in [-1, 1].</summary>
    public (float Left, float Right) PanGains()
    {
        double angle = (Pan + 1.0) * Math.PI / 4.0; // 0..pi/2
        return ((float)Math.Cos(angle), (float)Math.Sin(angle));
    }
}
