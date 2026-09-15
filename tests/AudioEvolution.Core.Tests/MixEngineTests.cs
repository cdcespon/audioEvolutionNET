using AudioEvolution.Core.Engine;
using AudioEvolution.Core.Model;
using AudioEvolution.Core.Time;
using Xunit;

namespace AudioEvolution.Core.Tests;

public class MixEngineTests
{
    private static AudioClip MakeClip(string sourceId, long timelinePos, int rate, float[] mono)
    {
        return new AudioClip
        {
            Name = "clip",
            SourceFilePath = sourceId,
            SourceStart = SampleTime.Zero(rate),
            SourceLength = new SampleTime(mono.Length, rate),
            TimelinePosition = new SampleTime(timelinePos, rate)
        };
    }

    private static Func<string, IAudioClipSource> ResolverFor(Dictionary<string, float[]> sources, int rate) =>
        id => new InMemoryClipSource(rate, 1, sources[id]);

    [Fact]
    public void TrackRenderer_SumsTwoOverlappingClips()
    {
        const int rate = 48000;
        var sources = new Dictionary<string, float[]>
        {
            ["a"] = new float[10],
            ["b"] = new float[10]
        };
        Array.Fill(sources["a"], 0.2f);
        Array.Fill(sources["b"], 0.3f);

        var track = new Track { Name = "t1", Type = TrackType.Audio };
        track.Clips.Add(MakeClip("a", 0, rate, sources["a"]));
        track.Clips.Add(MakeClip("b", 0, rate, sources["b"])); // fully overlapping

        var renderer = new TrackRenderer(ResolverFor(sources, rate));
        Span<float> dest = new float[10 * 2];
        renderer.Render(track, 0, 10, rate, dest);

        // Pan center => equal power ~0.707 each channel; combined mono = 0.5
        float expectedMono = 0.5f;
        float expectedChannel = expectedMono * MathF.Cos(MathF.PI / 4);
        Assert.Equal(expectedChannel, dest[0], precision: 4);
        Assert.Equal(expectedChannel, dest[1], precision: 4);
    }

    [Fact]
    public void TrackRenderer_ClipsPastEndOfTimelineWindow_AreIgnored()
    {
        const int rate = 48000;
        var sources = new Dictionary<string, float[]> { ["a"] = new float[10] };
        Array.Fill(sources["a"], 1.0f);

        var track = new Track { Name = "t1", Type = TrackType.Audio };
        track.Clips.Add(MakeClip("a", 1000, rate, sources["a"]));

        var renderer = new TrackRenderer(ResolverFor(sources, rate));
        Span<float> dest = new float[10 * 2];
        renderer.Render(track, 0, 10, rate, dest);

        foreach (var sample in dest)
            Assert.Equal(0f, sample);
    }

    [Fact]
    public void MixEngine_Solo_SilencesNonSoloedTracks()
    {
        const int rate = 48000;
        var sources = new Dictionary<string, float[]>
        {
            ["a"] = new float[10],
            ["b"] = new float[10]
        };
        Array.Fill(sources["a"], 1.0f);
        Array.Fill(sources["b"], 1.0f);

        var trackA = new Track { Name = "A", Type = TrackType.Audio, Soloed = true };
        trackA.Clips.Add(MakeClip("a", 0, rate, sources["a"]));

        var trackB = new Track { Name = "B", Type = TrackType.Audio };
        trackB.Clips.Add(MakeClip("b", 0, rate, sources["b"]));

        var project = new Project { Name = "p", SampleRate = rate };
        project.Tracks.Add(trackA);
        project.Tracks.Add(trackB);

        var engine = new MixEngine(ResolverFor(sources, rate));
        Span<float> dest = new float[10 * 2];
        engine.RenderMix(project, 0, 10, dest);

        // Only trackA (soloed) should contribute.
        Assert.True(dest[0] > 0f);
        float onlyA = MathF.Cos(MathF.PI / 4); // pan-center gain for a single 1.0 mono source
        Assert.Equal(onlyA, dest[0], precision: 4);
    }

    [Fact]
    public void TrackRenderer_VolumeAutomation_OverridesStaticVolumePerSample()
    {
        const int rate = 48000;
        var sources = new Dictionary<string, float[]> { ["a"] = new float[10] };
        Array.Fill(sources["a"], 1.0f);

        // Static volume is silent (-96dB); automation ramps 0dB -> -96dB linearly across the buffer.
        // If automation weren't applied, every sample would be near-silent instead of following the ramp.
        var track = new Track { Name = "t1", Type = TrackType.Audio, VolumeDb = -96f };
        track.Clips.Add(MakeClip("a", 0, rate, sources["a"]));

        var lane = new AutomationLane { Target = AutomationTarget.Volume };
        lane.AddPoint(new AutomationPoint(new SampleTime(0, rate), 0f));
        lane.AddPoint(new AutomationPoint(new SampleTime(9, rate), -96f));
        track.AutomationLanes.Add(lane);

        var renderer = new TrackRenderer(ResolverFor(sources, rate));
        Span<float> dest = new float[10 * 2];
        renderer.Render(track, 0, 10, rate, dest);

        // Sample 0 should be at full (0dB) volume, sample 9 near-silent -> first sample much louder than last.
        float firstSampleLevel = MathF.Abs(dest[0]);
        float lastSampleLevel = MathF.Abs(dest[18]);
        Assert.True(firstSampleLevel > 0.5f, $"expected loud first sample, got {firstSampleLevel}");
        Assert.True(lastSampleLevel < 0.01f, $"expected near-silent last sample, got {lastSampleLevel}");
    }

    [Fact]
    public void AudioClip_FadeIn_RampsGainFromZero()
    {
        var clip = new AudioClip
        {
            Name = "c",
            SourceFilePath = "x",
            SourceStart = SampleTime.Zero(48000),
            SourceLength = new SampleTime(100, 48000),
            TimelinePosition = SampleTime.Zero(48000),
            FadeInSamples = 10,
            FadeInCurve = FadeCurve.Linear
        };

        Assert.Equal(0f, clip.GainAt(0), precision: 4);
        Assert.Equal(0.5f, clip.GainAt(5), precision: 4);
        Assert.Equal(1f, clip.GainAt(10), precision: 4);
    }
}
