// -----------------------------------------------------------------------------
// VoiceCommandLine.cs
// Author: Kurt Mitchell
//
// Pure, testable parsing of the speech-engine command-line arguments:
//   --stt system | whisper        pick the recognizer (default: system)
//   --whisper-model <path>        ggml model path for Whisper
// The model path falls back to the RAYNEO_WHISPER_MODEL environment variable.
// Kept free of WPF/app state so it unit-tests without launching the overlay.
// -----------------------------------------------------------------------------

using System;

namespace Infinyte.RayNeo.Hud.Voice;

/// <summary>The recognizer selection resolved from the command line.</summary>
/// <param name="Engine">The chosen speech-to-text backend.</param>
/// <param name="WhisperModelPath">
/// The ggml model path when <paramref name="Engine"/> is
/// <see cref="SpeechEngineKind.Whisper"/>; otherwise null.
/// </param>
public readonly record struct SpeechEngineSelection(SpeechEngineKind Engine, string? WhisperModelPath);

/// <summary>Parses the speech-engine arguments for <c>App</c>.</summary>
public static class VoiceCommandLine
{
    /// <summary>Environment variable consulted when <c>--whisper-model</c> is absent.</summary>
    public const string ModelPathEnvironmentVariable = "RAYNEO_WHISPER_MODEL";

    /// <summary>
    /// Resolves the speech engine and Whisper model path from
    /// <paramref name="args"/>. Defaults to <see cref="SpeechEngineKind.System"/>.
    /// When <c>--whisper-model</c> is not supplied, the model path falls back to
    /// the <see cref="ModelPathEnvironmentVariable"/> environment variable.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="environment">
    /// Environment-variable lookup (defaults to
    /// <see cref="Environment.GetEnvironmentVariable(string)"/>); injectable for tests.
    /// </param>
    /// <returns>The resolved engine and model path.</returns>
    /// <exception cref="ArgumentException">
    /// <c>--stt</c> is given an unrecognized value.
    /// </exception>
    public static SpeechEngineSelection ParseSpeechEngine(
        string[] args, Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        environment ??= Environment.GetEnvironmentVariable;

        SpeechEngineKind engine = SpeechEngineKind.System;
        string? modelPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--stt" when i + 1 < args.Length:
                    engine = ParseEngine(args[i + 1]);
                    break;
                case "--whisper-model" when i + 1 < args.Length:
                    string value = args[i + 1].Trim();
                    if (value.Length > 0)
                    {
                        modelPath = value;
                    }
                    break;
            }
        }

        // Fall back to the environment variable only when no explicit path was given.
        if (modelPath is null)
        {
            string? fromEnv = environment(ModelPathEnvironmentVariable)?.Trim();
            if (!string.IsNullOrEmpty(fromEnv))
            {
                modelPath = fromEnv;
            }
        }

        return new SpeechEngineSelection(engine, modelPath);
    }

    private static SpeechEngineKind ParseEngine(string value) => value.Trim().ToLowerInvariant() switch
    {
        "system" => SpeechEngineKind.System,
        "whisper" => SpeechEngineKind.Whisper,
        _ => throw new ArgumentException(
            $"Unknown --stt value '{value}'. Use 'system' or 'whisper'.", nameof(value)),
    };
}
