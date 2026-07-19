// -----------------------------------------------------------------------------
// App.xaml.cs
// Author: Kurt Mitchell
//
// Startup: parse args, pick the orientation source (live glasses or simulated),
// pick the display and push-to-talk key, then show the overlay window.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using Infinyte.RayNeo;
using Infinyte.RayNeo.Hud.Display;
using Infinyte.RayNeo.Hud.Voice;

namespace Infinyte.RayNeo.Hud;

/// <summary>WPF application entry point for the RayNeo HUD overlay.</summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        int? requestedDisplay = ParseDisplayArg(e.Args);
        VoiceOptions voiceOptions = ParsePttArg(e.Args);
        voiceOptions = ApplySpeechEngineArgs(e.Args, voiceOptions);

        IReadOnlyList<DisplayInfo> displays = DisplayEnumerator.All();
        DisplaySelection selection = DisplayLocator.Choose(displays, requestedDisplay);

        IHeadOrientationProvider provider = CreateProvider(out string? deviceWarning);
        string? warning = CombineWarnings(deviceWarning, selection.Warning);

        LogStartup(displays, selection, deviceWarning, voiceOptions);

        var window = new MainWindow(selection.Display, provider, warning, voiceOptions);
        window.Show();
    }

    // A one-line-per-display startup log to %TEMP%\rayneo-hud.log so overlay
    // placement can be diagnosed without a console (this is a windowed app).
    private static void LogStartup(
        IReadOnlyList<DisplayInfo> displays, DisplaySelection selection, string? deviceWarning,
        VoiceOptions voiceOptions)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss}] RayNeo HUD startup");
            sb.AppendLine($"  device: {(deviceWarning is null ? "glasses connected" : deviceWarning)}");
            sb.AppendLine($"  push-to-talk: {voiceOptions.KeyName} (vk 0x{voiceOptions.VirtualKey:X2})");
            sb.AppendLine($"  displays ({displays.Count}):");
            foreach (DisplayInfo d in displays)
            {
                sb.AppendLine($"    {d}");
            }
            sb.AppendLine($"  chosen: {selection.Display}");
            sb.AppendLine($"  warning: {selection.Warning ?? "(none)"}");
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "rayneo-hud.log"), sb.ToString());
        }
        catch
        {
            // Diagnostics only — never let logging break startup.
        }
    }

    // Opens the glasses if present; otherwise falls back to the simulator so the
    // HUD still runs (CLAUDE.md: everything but the live demo runs device-free).
    private static IHeadOrientationProvider CreateProvider(out string? warning)
    {
        try
        {
            RayNeoClient client = RayNeoClient.Open();
            warning = null;
            return new DeviceOrientationProvider(client);
        }
        catch (InvalidOperationException)
        {
            warning = "Glasses not found — running with simulated motion.";
            return new SimulatedOrientationProvider();
        }
    }

    private static string? CombineWarnings(string? deviceWarning, string? displayWarning)
    {
        if (deviceWarning is null)
        {
            return displayWarning;
        }
        return displayWarning is null ? deviceWarning : $"{deviceWarning}  |  {displayWarning}";
    }

    // --stt whisper --whisper-model <path> — pick the recognizer. An unknown
    // --stt value falls back to System.Speech so a typo never leaves voice
    // unusable; VoiceRuntime surfaces any Whisper model problem on the glass.
    private static VoiceOptions ApplySpeechEngineArgs(string[] args, VoiceOptions options)
    {
        try
        {
            SpeechEngineSelection selection = VoiceCommandLine.ParseSpeechEngine(args);
            return options with { Engine = selection.Engine, WhisperModelPath = selection.WhisperModelPath };
        }
        catch (ArgumentException)
        {
            return options; // keep the default System engine
        }
    }

    private static int? ParseDisplayArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if ((args[i] == "--display" || args[i] == "-d") && int.TryParse(args[i + 1], out int index))
            {
                return index;
            }
        }
        return null;
    }

    // --ptt F8 | --ptt F13 | --ptt 0x77 — hold-to-talk key override. Unknown
    // values fall back to the default so a typo never leaves voice unusable.
    private static VoiceOptions ParsePttArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "--ptt")
            {
                continue;
            }
            string value = args[i + 1].Trim();

            // Function keys F1..F24 (VK_F1 = 0x70).
            if ((value.StartsWith('F') || value.StartsWith('f')) &&
                int.TryParse(value.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
            {
                return new VoiceOptions(0x70 + fn - 1, $"F{fn}");
            }

            // Raw virtual-key code, e.g. 0x77.
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int vk) &&
                vk is > 0 and <= 0xFE)
            {
                return new VoiceOptions(vk, value.ToUpperInvariant());
            }
        }
        return VoiceOptions.Default;
    }
}
