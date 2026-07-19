// -----------------------------------------------------------------------------
// VoiceToolArguments.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System.Text.Json;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// Thrown when a tool's JSON arguments are missing a required property or have
/// the wrong type. The message is written for the model, which sees it as an
/// error tool-result and can correct the call.
/// </summary>
public sealed class VoiceToolArgumentException : Exception
{
    /// <summary>Creates the exception with a model-readable message.</summary>
    public VoiceToolArgumentException(string message) : base(message) { }
}

/// <summary>
/// Typed accessors over a tool call's JSON argument object. Required accessors
/// throw <see cref="VoiceToolArgumentException"/> with a message naming the
/// offending property; optional accessors fall back to a default. A non-object
/// root (the model sent null or something malformed) behaves like an empty
/// object.
/// </summary>
public sealed class VoiceToolArguments
{
    private readonly JsonElement _root;
    private readonly bool _isObject;

    /// <summary>Wraps the JSON argument object supplied by the model.</summary>
    public VoiceToolArguments(JsonElement root)
    {
        _root = root;
        _isObject = root.ValueKind == JsonValueKind.Object;
    }

    /// <summary>Reads a required string property.</summary>
    public string GetRequiredString(string name) =>
        TryGet(name, JsonValueKind.String, out JsonElement value)
            ? value.GetString()!
            : throw Missing(name, "string");

    /// <summary>Reads a required number property.</summary>
    public double GetRequiredNumber(string name) =>
        TryGet(name, JsonValueKind.Number, out JsonElement value)
            ? value.GetDouble()
            : throw Missing(name, "number");

    /// <summary>Reads a required boolean property.</summary>
    public bool GetRequiredBoolean(string name)
    {
        if (_isObject && _root.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }
        throw Missing(name, "boolean");
    }

    /// <summary>Reads an optional string property, returning <paramref name="defaultValue"/> when absent or null.</summary>
    public string GetOptionalString(string name, string defaultValue) =>
        TryGet(name, JsonValueKind.String, out JsonElement value) ? value.GetString()! : defaultValue;

    /// <summary>Reads an optional number property, returning <paramref name="defaultValue"/> when absent or null.</summary>
    public double GetOptionalNumber(string name, double defaultValue) =>
        TryGet(name, JsonValueKind.Number, out JsonElement value) ? value.GetDouble() : defaultValue;

    private bool TryGet(string name, JsonValueKind kind, out JsonElement value)
    {
        if (_isObject && _root.TryGetProperty(name, out value) && value.ValueKind == kind)
        {
            return true;
        }
        value = default;
        return false;
    }

    private static VoiceToolArgumentException Missing(string name, string type) =>
        new($"Argument '{name}' is required and must be a {type}.");
}
