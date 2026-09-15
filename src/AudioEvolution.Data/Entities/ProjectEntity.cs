namespace AudioEvolution.Data.Entities;

/// <summary>
/// EF Core persistence shape for a Project. Kept separate from AudioEvolution.Core.Model.Project
/// so the domain model has zero dependency on EF Core/SQLite; ProjectMapper converts between
/// the two. Track/Clip/Automation data is stored as JSON blobs for now — normalizing them into
/// full relational tables is worthwhile once querying across projects (e.g. "find all clips
/// referencing file X") is actually needed, not before.
/// </summary>
public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int SampleRate { get; set; }
    public int BitDepth { get; set; }
    public double Tempo { get; set; }
    public int TimeSignatureNumerator { get; set; }
    public int TimeSignatureDenominator { get; set; }
    public required string TracksJson { get; set; }
    public required string MarkersJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
}
