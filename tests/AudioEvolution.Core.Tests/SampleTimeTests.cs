using AudioEvolution.Core.Time;
using Xunit;

namespace AudioEvolution.Core.Tests;

public class SampleTimeTests
{
    [Fact]
    public void FromSeconds_RoundTrips_ThroughSamples()
    {
        var t = SampleTime.FromSeconds(1.5, 48000);
        Assert.Equal(72000, t.Samples);
        Assert.Equal(1.5, t.TotalSeconds, precision: 10);
    }

    [Fact]
    public void ConvertTo_PreservesApproximateDuration()
    {
        var t = SampleTime.FromSeconds(2.0, 44100);
        var converted = t.ConvertTo(48000);
        Assert.Equal(96000, converted.Samples);
    }

    [Fact]
    public void Addition_ThrowsOnMismatchedSampleRates()
    {
        var a = new SampleTime(100, 48000);
        var b = new SampleTime(100, 44100);
        Assert.Throws<InvalidOperationException>(() => a + b);
    }

    [Fact]
    public void Comparison_WorksAcrossOperators()
    {
        var a = new SampleTime(100, 48000);
        var aCopy = new SampleTime(100, 48000);
        var b = new SampleTime(200, 48000);
        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= aCopy);
        Assert.True(a >= aCopy);
    }
}
