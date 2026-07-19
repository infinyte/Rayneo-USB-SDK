// -----------------------------------------------------------------------------
// DelegateVoiceTool.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System.Text.Json;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// An <see cref="IVoiceTool"/> defined by a delegate — the one-class way to
/// declare simple tools. The HUD layer uses this to expose Windows-side
/// capabilities (pins, app launching) without new tool classes; the built-in
/// factories in <see cref="TimerTools"/> and <see cref="SessionTools"/> use it
/// too.
/// </summary>
public sealed class DelegateVoiceTool : IVoiceTool
{
    private readonly Func<VoiceToolArguments, CancellationToken, Task<string>> _callback;

    /// <summary>Creates a tool from its declaration and an execution callback.</summary>
    public DelegateVoiceTool(
        string name,
        string description,
        IReadOnlyList<VoiceToolParameter> parameters,
        Func<VoiceToolArguments, CancellationToken, Task<string>> callback)
    {
        Name = name;
        Description = description;
        Parameters = parameters;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Description { get; }

    /// <inheritdoc/>
    public IReadOnlyList<VoiceToolParameter> Parameters { get; }

    /// <inheritdoc/>
    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        _callback(new VoiceToolArguments(arguments), cancellationToken);
}
