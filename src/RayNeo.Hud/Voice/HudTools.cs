// -----------------------------------------------------------------------------
// HudTools.cs
// Author: Kurt Mitchell
//
// The Windows/HUD-side voice tools: world-anchored pins and app/URL launching.
// Declared with DelegateVoiceTool so RayNeo.Voice stays platform-neutral.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Infinyte.RayNeo.Hud.Voice;

using Infinyte.RayNeo.Voice;

/// <summary>Factory for the HUD-side tool set.</summary>
public static class HudTools
{
    /// <summary>Creates the <c>pin_note</c> tool over <paramref name="pins"/>.</summary>
    public static IVoiceTool CreatePinNote(PinSurface pins) => new DelegateVoiceTool(
        "pin_note",
        "Pin a short note into the wearer's space. It stays anchored to that direction " +
        "in the room as they look around.",
        new[]
        {
            new VoiceToolParameter("text", "The note text (a few words).",
                VoiceToolParameterType.String, IsRequired: true),
            new VoiceToolParameter("direction",
                "Where to pin it relative to the current gaze: ahead, left, right, up, or down.",
                VoiceToolParameterType.String, IsRequired: false),
        },
        (args, _) => Task.FromResult(pins.AddPin(
            args.GetRequiredString("text"),
            args.GetOptionalString("direction", "ahead"))));

    /// <summary>Creates the <c>list_pins</c> tool.</summary>
    public static IVoiceTool CreateListPins(PinSurface pins) => new DelegateVoiceTool(
        "list_pins",
        "List the notes currently pinned in the wearer's space.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) => Task.FromResult(pins.ListPins()));

    /// <summary>Creates the <c>clear_pins</c> tool.</summary>
    public static IVoiceTool CreateClearPins(PinSurface pins) => new DelegateVoiceTool(
        "clear_pins",
        "Remove all pinned notes from the wearer's space.",
        Array.Empty<VoiceToolParameter>(),
        (_, _) => Task.FromResult(pins.ClearPins()));

    /// <summary>Creates the <c>open_app_or_url</c> tool.</summary>
    public static IVoiceTool CreateOpenAppOrUrl() => new DelegateVoiceTool(
        "open_app_or_url",
        "Open an application or website on the wearer's PC. Pass a program name " +
        "(e.g. 'notepad', 'devenv') or a full URL (e.g. 'https://youtube.com').",
        new[]
        {
            new VoiceToolParameter("target", "Program name/path or URL to open.",
                VoiceToolParameterType.String, IsRequired: true),
        },
        (args, _) =>
        {
            string target = args.GetRequiredString("target").Trim();
            try
            {
                // UseShellExecute resolves PATH entries, app aliases, and URLs
                // exactly like the Run dialog does.
                Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
                return Task.FromResult($"Opened '{target}'.");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Could not open '{target}': {ex.Message}");
            }
        });
}
