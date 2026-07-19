// -----------------------------------------------------------------------------
// ToolActivityEventArgs.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Voice;

/// <summary>Lifecycle of one tool execution, as surfaced to the HUD.</summary>
public enum ToolActivityStatus
{
    /// <summary>The tool call is about to execute.</summary>
    Started,

    /// <summary>The tool completed and returned a result.</summary>
    Succeeded,

    /// <summary>The tool threw; the model receives an error result.</summary>
    Failed,
}

/// <summary>
/// Raised by <see cref="AssistantToolLoop"/> around each tool execution so the
/// HUD can show live activity ("⚙ start_timer…") while the wearer waits.
/// </summary>
public sealed class ToolActivityEventArgs : EventArgs
{
    /// <summary>Creates the event payload.</summary>
    public ToolActivityEventArgs(string toolName, ToolActivityStatus status)
    {
        ToolName = toolName;
        Status = status;
    }

    /// <summary>Name of the tool being executed.</summary>
    public string ToolName { get; }

    /// <summary>Where the execution is in its lifecycle.</summary>
    public ToolActivityStatus Status { get; }
}
