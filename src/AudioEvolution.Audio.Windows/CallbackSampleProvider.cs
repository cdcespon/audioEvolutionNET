using AudioEvolution.Core.Engine;
using NAudio.Wave;

namespace AudioEvolution.Audio.Windows;

/// <summary>
/// Adapts our pull-based <see cref="AudioCallback"/> (frame-oriented, interleaved stereo
/// float32) to NAudio's <see cref="ISampleProvider"/> (sample-oriented: NAudio's playback
/// thread calls Read() whenever WASAPI's buffer needs refilling).
/// </summary>
internal sealed class CallbackSampleProvider : ISampleProvider
{
    private readonly AudioCallback _callback;
    private long _nextFrame;

    public WaveFormat WaveFormat { get; }

    public CallbackSampleProvider(int sampleRate, AudioCallback callback)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels: 2);
        _callback = callback;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        int frameCount = count / channels;
        int sampleCount = frameCount * channels;

        _callback(_nextFrame, frameCount, buffer.AsSpan(offset, sampleCount));
        _nextFrame += frameCount;

        return sampleCount;
    }
}
