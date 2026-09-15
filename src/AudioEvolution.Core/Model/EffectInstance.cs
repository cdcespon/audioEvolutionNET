namespace AudioEvolution.Core.Model;

/// <summary>
/// Placeholder for an effect slot in a track's processing chain. Concrete DSP processors
/// implement AudioEvolution.Core.Engine.IEffectProcessor and are looked up by PluginId
/// through the effect registry at graph-build time — kept separate from this record so the
/// domain model has no dependency on the audio engine or on any specific plugin format.
/// </summary>
public sealed class EffectInstance
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string PluginId { get; set; }
    public bool Bypassed { get; set; }
    // `init`, not a bare `get`: same System.Text.Json get-only-collection gap as Track/Project.
    public Dictionary<string, float> Parameters { get; init; } = new();
}
