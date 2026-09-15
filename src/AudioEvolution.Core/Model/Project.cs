namespace AudioEvolution.Core.Model;

public sealed class ProjectMarker
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required Time.SampleTime Position { get; set; }
}

public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public int SampleRate { get; set; } = 48000;
    public int BitDepth { get; set; } = 24;
    public double Tempo { get; set; } = 120.0;
    public (int Numerator, int Denominator) TimeSignature { get; set; } = (4, 4);

    // `init` (not a bare `get`) so System.Text.Json's reflection deserializer actually
    // populates these on load instead of silently leaving them empty.
    public List<Track> Tracks { get; init; } = new();
    public List<ProjectMarker> Markers { get; init; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
}
