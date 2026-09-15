using AudioEvolution.Audio.Windows;

namespace AudioEvolution.Audio.Windows.Tests;

public class CallbackSampleProviderTests
{
    [Fact]
    public void WaveFormat_IsInterleavedStereoFloat32AtRequestedSampleRate()
    {
        var provider = new CallbackSampleProvider(48000, (_, _, _) => { });

        Assert.Equal(48000, provider.WaveFormat.SampleRate);
        Assert.Equal(2, provider.WaveFormat.Channels);
        Assert.Equal(NAudio.Wave.WaveFormatEncoding.IeeeFloat, provider.WaveFormat.Encoding);
        Assert.Equal(32, provider.WaveFormat.BitsPerSample);
    }

    [Fact]
    public void Read_PassesCorrectFrameCountAndBufferSliceToCallback()
    {
        long? capturedStartFrame = null;
        int? capturedFrameCount = null;
        int? capturedSpanLength = null;

        var provider = new CallbackSampleProvider(48000, (startFrame, frameCount, buffer) =>
        {
            capturedStartFrame = startFrame;
            capturedFrameCount = frameCount;
            capturedSpanLength = buffer.Length;
        });

        var buffer = new float[512 * 2];
        int read = provider.Read(buffer, offset: 0, count: 512 * 2);

        Assert.Equal(1024, read);
        Assert.Equal(0, capturedStartFrame);
        Assert.Equal(512, capturedFrameCount);
        Assert.Equal(1024, capturedSpanLength);
    }

    [Fact]
    public void Read_AdvancesFramePositionAcrossSuccessiveCalls()
    {
        var startFrames = new List<long>();
        var provider = new CallbackSampleProvider(48000, (startFrame, _, _) => startFrames.Add(startFrame));

        var buffer = new float[256 * 2];
        provider.Read(buffer, 0, 256 * 2);
        provider.Read(buffer, 0, 256 * 2);
        provider.Read(buffer, 0, 128 * 2);
        provider.Read(buffer, 0, 128 * 2);

        Assert.Equal(new long[] { 0, 256, 512, 640 }, startFrames);
    }

    [Fact]
    public void Read_HonorsNonZeroOffsetIntoDestinationBuffer()
    {
        var provider = new CallbackSampleProvider(48000, (_, frameCount, destination) =>
        {
            for (int i = 0; i < destination.Length; i++)
                destination[i] = 1f;
        });

        // Buffer bigger than what's requested, with a non-zero offset — Read must write
        // only into the requested slice, not from index 0.
        var buffer = new float[20];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = -1f;

        int read = provider.Read(buffer, offset: 4, count: 10);

        Assert.Equal(10, read);
        for (int i = 0; i < 4; i++) Assert.Equal(-1f, buffer[i]);
        for (int i = 4; i < 14; i++) Assert.Equal(1f, buffer[i]);
        for (int i = 14; i < 20; i++) Assert.Equal(-1f, buffer[i]);
    }

    [Fact]
    public void Read_TruncatesOddSampleCountToWholeFrames()
    {
        int? capturedFrameCount = null;
        var provider = new CallbackSampleProvider(48000, (_, frameCount, _) => capturedFrameCount = frameCount);

        var buffer = new float[10];
        int read = provider.Read(buffer, 0, count: 9);

        // count=9 is not a whole number of stereo frames (9/2=4 with 1 sample left over) —
        // the callback must only ever see whole frames, and Read must report what it
        // actually filled (8), not the requested odd count.
        Assert.Equal(4, capturedFrameCount);
        Assert.Equal(8, read);
    }
}
