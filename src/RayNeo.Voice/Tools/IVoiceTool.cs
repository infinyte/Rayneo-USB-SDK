// -----------------------------------------------------------------------------
// IVoiceTool.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System.Text.Json;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// A capability the assistant can invoke during a voice turn (start a timer,
/// pin a note, open an app). Tools are engine-agnostic: the declaration here is
/// translated to the model API's tool schema by the transport, and
/// <see cref="AssistantToolLoop"/> executes calls the model makes. The returned
/// string is fed back to the model verbatim, so it should read as a short,
/// factual result ("Started timer 'tea' for 3:00.").
/// </summary>
public interface IVoiceTool
{
    /// <summary>Unique tool name (letters, digits, underscore, hyphen; max 64 chars).</summary>
    string Name { get; }

    /// <summary>What the tool does and when the model should call it.</summary>
    string Description { get; }

    /// <summary>The tool's input parameters (empty when it takes none).</summary>
    IReadOnlyList<VoiceToolParameter> Parameters { get; }

    /// <summary>
    /// Executes the tool with the model-supplied <paramref name="arguments"/>
    /// (a JSON object). Throw <see cref="VoiceToolArgumentException"/> for bad
    /// input; any exception is reported back to the model as an error result
    /// rather than crashing the voice loop.
    /// </summary>
    Task<string> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}
