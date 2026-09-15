using System.Runtime.InteropServices;
using AudioEvolution.Audio.Windows;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace AudioEvolution.Audio.Windows.Tests;

/// <summary>
/// Exercises the real WASAPI backend against whatever audio hardware exists on the machine
/// running the tests. Not mockable and not something that "should work" on faith — this
/// renders a real tone through the default output device and independently verifies it via
/// WASAPI loopback capture (records what actually reaches the OS mix, downstream of our
/// code entirely). Requires a real Windows machine with at least one active render device;
/// skips itself cleanly if none is found (e.g. a headless CI runner) instead of failing.
///
/// Loopback capture happens post-mixer, so it reflects the master volume/mute state. To keep
/// the assertion meaningful regardless of how the machine's volume happens to be set, the
/// test saves the master volume/mute, forces a low-but-nonzero level for its own duration,
/// and restores the original state afterward no matter what (try/finally) — so it can leave
/// a brief, quiet, real tone audible on the machine while it runs.
/// </summary>
public class WasapiHardwareIntegrationTests
{
    [Fact]
    public void ListOutputDevices_ReturnsAtLeastOneRealDevice()
    {
        var devices = new WasapiDeviceEnumerator().ListOutputDevices();

        if (devices.Count == 0)
        {
            return; // Entorno sin dispositivo de audio activo (p.ej. runner headless) — no es un fallo del código.
        }

        Assert.All(devices, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Id));
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.True(d.MaxChannels > 0);
            Assert.NotEmpty(d.SupportedSampleRates);
        });
    }

    [Fact]
    public void Playback_RealToneReachesOsLoopbackCapture()
    {
        using var deviceEnumerator = new MMDeviceEnumerator();
        MMDevice? defaultDevice;
        try
        {
            defaultDevice = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (COMException)
        {
            return; // No hay dispositivo de salida por defecto en esta máquina — no es un fallo del código.
        }

        using var deviceHandle = defaultDevice;
        var endpointVolume = defaultDevice.AudioEndpointVolume;
        bool originalMute = endpointVolume.Mute;
        float originalVolume = endpointVolume.MasterVolumeLevelScalar;

        const int sampleRate = 48000;
        const double toneHz = 440.0;
        const float amplitude = 0.05f; // bajo a propósito: solo tiene que ser detectable, no molesto.

        var capturedSamples = new List<float>();
        using var loopback = new WasapiLoopbackCapture(defaultDevice);
        loopback.DataAvailable += (_, e) =>
        {
            // e.Buffer es PCM en el formato de loopback.WaveFormat (float32 en Windows moderno).
            int sampleCount = e.BytesRecorded / sizeof(float);
            for (int i = 0; i < sampleCount; i++)
                capturedSamples.Add(BitConverter.ToSingle(e.Buffer, i * sizeof(float)));
        };

        var enumerator = new WasapiDeviceEnumerator();
        using var device = enumerator.OpenOutput(defaultDevice.ID, sampleRate, bufferSizeFrames: 1024);

        try
        {
            endpointVolume.Mute = false;
            endpointVolume.MasterVolumeLevelScalar = Math.Max(originalVolume, 0.15f);

            double phase = 0.0;
            double phaseStep = 2.0 * Math.PI * toneHz / sampleRate;

            device.Start((startFrame, frameCount, buffer) =>
            {
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float sample = amplitude * (float)Math.Sin(phase);
                    phase += phaseStep;
                    if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;

                    buffer[frame * 2] = sample;
                    buffer[frame * 2 + 1] = sample;
                }
            });

            loopback.StartRecording();
            Thread.Sleep(500);
            loopback.StopRecording();

            device.Stop();
        }
        finally
        {
            endpointVolume.Mute = originalMute;
            endpointVolume.MasterVolumeLevelScalar = originalVolume;
        }

        Assert.True(capturedSamples.Count > 0, "El loopback no capturó ninguna muestra.");

        double sumSquares = 0.0;
        foreach (float s in capturedSamples) sumSquares += (double)s * s;
        double rms = Math.Sqrt(sumSquares / capturedSamples.Count);

        // Piso deliberadamente bajo (silencio digital puro da RMS=0): confirma que hay
        // energía real en la señal capturada por el SO, no que el tono se reprodujo "limpio"
        // (el pipeline de audio del SO puede aplicar EQ/limitador de por medio).
        Assert.True(rms > 0.001, $"RMS capturado por loopback demasiado bajo ({rms:F6}) — no llegó audio real al mix del SO.");
    }
}
