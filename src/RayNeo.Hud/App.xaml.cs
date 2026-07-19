// -----------------------------------------------------------------------------
// App.xaml.cs
// Author: Kurt Mitchell
//
// Startup: parse args, pick the orientation source (live glasses or simulated),
// pick the display, then show the overlay window.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Windows;
using Infinyte.RayNeo;
using Infinyte.RayNeo.Hud.Display;

namespace Infinyte.RayNeo.Hud;

/// <summary>WPF application entry point for the RayNeo HUD overlay.</summary>
public partial class App : Application
{
    /// <inheritdoc/>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        int? requestedDisplay = ParseDisplayArg(e.Args);

        IReadOnlyList<DisplayInfo> displays = DisplayEnumerator.All();
        DisplaySelection selection = DisplayLocator.Choose(displays, requestedDisplay);

        IHeadOrientationProvider provider = CreateProvider(out string? deviceWarning);
        string? warning = CombineWarnings(deviceWarning, selection.Warning);

        var window = new MainWindow(selection.Display, provider, warning);
        window.Show();
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
}
