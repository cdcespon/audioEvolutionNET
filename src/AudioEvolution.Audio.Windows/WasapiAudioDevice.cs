using AudioEvolution.Core.Engine;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioEvolution.Audio.Windows;

/// <summary>
/// Realtime WASAPI output device (shared mode). Owns the <see cref="MMDevice"/> passed to it —
/// disposed together with this instance.
/// </summary>
public sealed class WasapiAudioDevice : IAudioDevice
{
    private readonly MMDevice _device;
    private WasapiOut? _wasapiOut;
    private bool _disposed;

    public AudioDeviceInfo Info { get; }
    public int SampleRate { get; }
    public int BufferSizeFrames { get; }
    public bool IsRunning { get; private set; }

    internal WasapiAudioDevice(MMDevice device, AudioDeviceInfo info, int sampleRate, int bufferSizeFrames)
    {
        _device = device;
        Info = info;
        SampleRate = sampleRate;
        BufferSizeFrames = bufferSizeFrames;
    }

    public void Start(AudioCallback callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) throw new InvalidOperationException("El dispositivo ya esta corriendo.");

        int latencyMs = Math.Max(1, (int)Math.Round(BufferSizeFrames * 1000.0 / SampleRate));
        var wasapiOut = new WasapiOut(_device, AudioClientShareMode.Shared, useEventSync: true, latency: latencyMs);
        wasapiOut.Init(new CallbackSampleProvider(SampleRate, callback));
        wasapiOut.Play();

        _wasapiOut = wasapiOut;
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = null;
        IsRunning = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _device.Dispose();
        _disposed = true;
    }
}
