using System.Text.Json;
using AudioEvolution.Core.Model;
using AudioEvolution.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AudioEvolution.Data.Repositories;

public sealed class ProjectRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = false,
        PropertyNamingPolicy = null
    };

    private readonly ProjectDbContext _db;

    public ProjectRepository(ProjectDbContext db)
    {
        _db = db;
    }

    public async Task SaveAsync(Project project, CancellationToken ct = default)
    {
        project.ModifiedAt = DateTimeOffset.UtcNow;

        var entity = await _db.Projects.FindAsync(new object[] { project.Id }, ct);
        if (entity is null)
        {
            entity = new ProjectEntity
            {
                Id = project.Id,
                Name = project.Name,
                TracksJson = "[]",
                MarkersJson = "[]",
                CreatedAt = project.CreatedAt,
                ModifiedAt = project.ModifiedAt
            };
            _db.Projects.Add(entity);
        }

        entity.Name = project.Name;
        entity.SampleRate = project.SampleRate;
        entity.BitDepth = project.BitDepth;
        entity.Tempo = project.Tempo;
        entity.TimeSignatureNumerator = project.TimeSignature.Numerator;
        entity.TimeSignatureDenominator = project.TimeSignature.Denominator;
        entity.TracksJson = JsonSerializer.Serialize(project.Tracks, JsonOptions);
        entity.MarkersJson = JsonSerializer.Serialize(project.Markers, JsonOptions);
        entity.ModifiedAt = project.ModifiedAt;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<Project?> LoadAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (entity is null) return null;

        var tracks = JsonSerializer.Deserialize<List<Track>>(entity.TracksJson, JsonOptions) ?? new();
        var markers = JsonSerializer.Deserialize<List<ProjectMarker>>(entity.MarkersJson, JsonOptions) ?? new();

        var project = new Project
        {
            Id = entity.Id,
            Name = entity.Name,
            SampleRate = entity.SampleRate,
            BitDepth = entity.BitDepth,
            Tempo = entity.Tempo,
            TimeSignature = (entity.TimeSignatureNumerator, entity.TimeSignatureDenominator),
            CreatedAt = entity.CreatedAt,
            ModifiedAt = entity.ModifiedAt
        };
        project.Tracks.AddRange(tracks);
        project.Markers.AddRange(markers);
        return project;
    }

    public async Task<List<(Guid Id, string Name, DateTimeOffset ModifiedAt)>> ListAsync(CancellationToken ct = default)
    {
        // SQLite has no native DateTimeOffset comparison, so EF Core can't translate an
        // ORDER BY on it into SQL — sort client-side on the (small, per-project) result set instead.
        var rows = await _db.Projects
            .AsNoTracking()
            .Select(p => new ValueTuple<Guid, string, DateTimeOffset>(p.Id, p.Name, p.ModifiedAt))
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Item3).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Projects.FindAsync(new object[] { id }, ct);
        if (entity is not null)
        {
            _db.Projects.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
