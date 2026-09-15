using AudioEvolution.Core.Codecs;
using Xunit;

namespace AudioEvolution.Core.Tests;

public class WavFileTests
{
    [Theory]
    [InlineData(WavSampleFormat.Pcm16)]
    [InlineData(WavSampleFormat.Pcm24)]
    [InlineData(WavSampleFormat.Pcm32)]
    [InlineData(WavSampleFormat.Float32)]
    public void WriteThenRead_RoundTripsWithinFormatPrecision(WavSampleFormat format)
    {
        var original = new float[] { 0f, 0.5f, -0.5f, 1f, -1f, 0.25f, -0.75f };
        var data = new WavAudioData(48000, 1, format, original);

        using var stream = new MemoryStream();
        WavFile.Write(stream, data);
        stream.Position = 0;
        var result = WavFile.Read(stream);

        Assert.Equal(48000, result.SampleRate);
        Assert.Equal(1, result.Channels);
        Assert.Equal(original.Length, result.Interleaved.Length);

        double tolerance = format switch
        {
            WavSampleFormat.Pcm16 => 1.0 / 32767,
            WavSampleFormat.Pcm24 => 1.0 / 8388607,
            _ => 1e-6
        };

        for (int i = 0; i < original.Length; i++)
            Assert.True(Math.Abs(original[i] - result.Interleaved[i]) <= tolerance + 1e-6,
                $"Sample {i}: expected {original[i]}, got {result.Interleaved[i]}");
    }

    [Fact]
    public void Write_StereoInterleaved_PreservesChannelOrder()
    {
        // L0 R0 L1 R1
        var original = new float[] { 0.1f, -0.1f, 0.2f, -0.2f };
        var data = new WavAudioData(44100, 2, WavSampleFormat.Float32, original);

        using var stream = new MemoryStream();
        WavFile.Write(stream, data);
        stream.Position = 0;
        var result = WavFile.Read(stream);

        Assert.Equal(2, result.Channels);
        Assert.Equal(2, result.FrameCount);
        Assert.Equal(original, result.Interleaved);
    }
}
