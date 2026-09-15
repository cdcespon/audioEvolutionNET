using AudioEvolution.Core.Engine;
using NAudio.CoreAudioApi;

namespace AudioEvolution.Audio.Windows;

public sealed class WasapiDeviceEnumerator : IAudioDeviceEnumerator
{
    public IReadOnlyList<AudioDeviceInfo> ListOutputDevices() => ListDevices(DataFlow.Render);

    public IReadOnlyList<AudioDeviceInfo> ListInputDevices() => ListDevices(DataFlow.Capture);

    public IAudioDevice OpenOutput(string deviceId, int sampleRate, int bufferSizeFrames)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        var info = ToDeviceInfo(device);
        return new WasapiAudioDevice(device, info, sampleRate, bufferSizeFrames);
    }

    private static IReadOnlyList<AudioDeviceInfo> ListDevices(DataFlow dataFlow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var result = new List<AudioDeviceInfo>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active))
        {
            using (device)
            {
                result.Add(ToDeviceInfo(device));
            }
        }
        return result;
    }

    private static AudioDeviceInfo ToDeviceInfo(MMDevice device)
    {
        var mixFormat = device.AudioClient.MixFormat;
        return new AudioDeviceInfo(
            device.ID,
            device.FriendlyName,
            AudioBackendKind.Wasapi,
            mixFormat.Channels,
            new[] { mixFormat.SampleRate });
    }
}
