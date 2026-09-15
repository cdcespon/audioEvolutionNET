namespace AudioEvolution.Core.Engine;

public enum AudioBackendKind
{
    Wasapi,
    Asio
}

public sealed record AudioDeviceInfo(string Id, string Name, AudioBackendKind Backend, int MaxChannels, int[] SupportedSampleRates);

/// <summary>
/// Abstraction over the realtime audio I/O backend. Concrete implementations (WASAPI via
/// NAudio.CoreAudioApi, ASIO via NAudio.Wave.Asio) live in AudioEvolution.Audio.Windows since
/// they are Windows-only and cannot be built or tested on this Linux dev container — this
/// interface is what lets AudioEvolution.Core and its tests stay platform-agnostic.
/// </summary>
public interface IAudioDevice : IDisposable
{
    AudioDeviceInfo Info { get; }
    int SampleRate { get; }
    int BufferSizeFrames { get; }
    bool IsRunning { get; }

    /// <summary>
    /// Called on the realtime audio thread for each buffer. Implementations must not allocate,
    /// lock, or do file/network I/O inside this callback.
    /// </summary>
    void Start(AudioCallback callback);

    void Stop();
}

/// <summary>
/// Realtime mix pull callback: fill <paramref name="outputBuffer"/> (interleaved, stereo) with
/// <paramref name="frameCount"/> frames of audio starting at the given master sample position.
/// </summary>
public delegate void AudioCallback(long startFrame, int frameCount, Span<float> outputBuffer);

public interface IAudioDeviceEnumerator
{
    IReadOnlyList<AudioDeviceInfo> ListOutputDevices();
    IReadOnlyList<AudioDeviceInfo> ListInputDevices();
    IAudioDevice OpenOutput(string deviceId, int sampleRate, int bufferSizeFrames);
}
