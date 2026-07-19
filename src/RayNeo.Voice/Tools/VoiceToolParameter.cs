// -----------------------------------------------------------------------------
// VoiceToolParameter.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>JSON type of a tool parameter (kept minimal on purpose).</summary>
public enum VoiceToolParameterType
{
    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A JSON number (integer or fraction).</summary>
    Number,

    /// <summary>A JSON boolean.</summary>
    Boolean,
}

/// <summary>
/// Declares one input parameter of an <see cref="IVoiceTool"/>. The transport
/// turns these declarations into the model API's JSON input schema, so tools
/// never depend on a specific model SDK.
/// </summary>
/// <param name="Name">Property name in the tool's JSON arguments.</param>
/// <param name="Description">What the parameter means, written for the model.</param>
/// <param name="Type">The parameter's JSON type.</param>
/// <param name="IsRequired">Whether the model must supply the parameter.</param>
public sealed record VoiceToolParameter(
    string Name,
    string Description,
    VoiceToolParameterType Type,
    bool IsRequired);
