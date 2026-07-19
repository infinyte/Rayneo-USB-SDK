// -----------------------------------------------------------------------------
// VoiceToolRegistry.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Infinyte.RayNeo.Voice;

/// <summary>
/// The set of tools offered to the assistant for a session. Registration order
/// is preserved (it is the order tools are presented to the model), names must
/// be unique and API-safe, and lookup is what <see cref="AssistantToolLoop"/>
/// uses to dispatch the model's tool calls.
/// </summary>
public sealed partial class VoiceToolRegistry
{
    private readonly List<IVoiceTool> _tools = new();
    private readonly Dictionary<string, IVoiceTool> _byName = new(StringComparer.Ordinal);

    /// <summary>The registered tools in registration order.</summary>
    public IReadOnlyList<IVoiceTool> Tools => _tools;

    /// <summary>Registers a tool.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="tool"/> is null.</exception>
    /// <exception cref="ArgumentException">The tool name is empty or not API-safe.</exception>
    /// <exception cref="InvalidOperationException">A tool with the same name is already registered.</exception>
    public void Register(IVoiceTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (string.IsNullOrWhiteSpace(tool.Name) || !ToolNamePattern().IsMatch(tool.Name))
        {
            throw new ArgumentException(
                $"Tool name '{tool.Name}' is invalid. Names use letters, digits, underscore, " +
                "or hyphen, and are at most 64 characters.", nameof(tool));
        }
        if (_byName.ContainsKey(tool.Name))
        {
            throw new InvalidOperationException($"A tool named '{tool.Name}' is already registered.");
        }
        _byName.Add(tool.Name, tool);
        _tools.Add(tool);
    }

    /// <summary>Looks up a tool by name.</summary>
    public bool TryGet(string name, out IVoiceTool? tool)
    {
        if (_byName.TryGetValue(name, out IVoiceTool? found))
        {
            tool = found;
            return true;
        }
        tool = null;
        return false;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ToolNamePattern();
}
