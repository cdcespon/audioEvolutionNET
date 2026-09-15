using AudioEvolution.Core.Model;
using AudioEvolution.Core.Time;
using AudioEvolution.Data;
using AudioEvolution.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AudioEvolution.Core.Tests;

public class ProjectRepositoryTests : IDisposable
{
    private readonly ProjectDbContext _db;
    private readonly ProjectRepository _repo;

    public ProjectRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new ProjectDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new ProjectRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveThenLoad_RoundTripsProjectWithTracksAndClips()
    {
        var project = new Project { Name = "Mi Cancion", SampleRate = 48000 };
        var track = new Track { Name = "Guitarra", Type = TrackType.Audio, VolumeDb = -3f, Pan = 0.5f };
        track.Clips.Add(new AudioClip
        {
            Name = "intro",
            SourceFilePath = "/audio/intro.wav",
            SourceStart = new SampleTime(2000, 48000),
            SourceLength = new SampleTime(48000, 48000),
            TimelinePosition = new SampleTime(144000, 48000), // nonzero: catches the SampleTime(0,0) default-on-load bug
            FadeInSamples = 480,
            FadeOutSamples = 960
        });
        project.Tracks.Add(track);
        project.Markers.Add(new ProjectMarker { Name = "Verse", Position = new SampleTime(96000, 48000) });

        await _repo.SaveAsync(project);
        var loaded = await _repo.LoadAsync(project.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Mi Cancion", loaded!.Name);
        Assert.Single(loaded.Tracks);
        Assert.Equal("Guitarra", loaded.Tracks[0].Name);
        Assert.Equal(-3f, loaded.Tracks[0].VolumeDb);
        Assert.Single(loaded.Tracks[0].Clips);
        var loadedClip = loaded.Tracks[0].Clips[0];
        Assert.Equal("/audio/intro.wav", loadedClip.SourceFilePath);
        Assert.Equal(2000, loadedClip.SourceStart.Samples);
        Assert.Equal(48000, loadedClip.SourceStart.SampleRate);
        Assert.Equal(144000, loadedClip.TimelinePosition.Samples);
        Assert.Equal(480, loadedClip.FadeInSamples);
        Assert.Equal(960, loadedClip.FadeOutSamples);
        Assert.Single(loaded.Markers);
        Assert.Equal(96000, loaded.Markers[0].Position.Samples);
    }

    [Fact]
    public async Task Delete_RemovesProjectFromStore()
    {
        var project = new Project { Name = "Temp", SampleRate = 44100 };
        await _repo.SaveAsync(project);

        await _repo.DeleteAsync(project.Id);
        var loaded = await _repo.LoadAsync(project.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task List_ReturnsProjectsOrderedByMostRecentlyModified()
    {
        var older = new Project { Name = "Older", SampleRate = 44100 };
        await _repo.SaveAsync(older);
        await Task.Delay(10);
        var newer = new Project { Name = "Newer", SampleRate = 44100 };
        await _repo.SaveAsync(newer);

        var list = await _repo.ListAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal("Newer", list[0].Name);
    }
}
