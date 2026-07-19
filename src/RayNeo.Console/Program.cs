// -----------------------------------------------------------------------------
// Program.cs
// Author: Kurt Mitchell
//
// Console demo for the RayNeo Air 4 Pro client. Two subcommands:
//   run       — live orientation readout (pitch / roll / yaw)
//   calibrate — measures the device tick rate, then walks the user through a
//               nod / shake / roll test to determine which gyro axis maps to
//               which motion.
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using Infinyte.RayNeo;

namespace Infinyte.RayNeo.Demo;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitBadArgs = 1;
    private const int ExitDeviceMissing = 2;

    private static int Main(string[] args)
    {
        string subcommand = args.Length == 0 ? "run" : args[0].ToLowerInvariant();

        try
        {
            return subcommand switch
            {
                "run" => RunLiveReadout(),
                "calibrate" => RunCalibration(),
                "--help" or "-h" or "help" => PrintUsage(ExitOk),
                _ => PrintUsage(ExitBadArgs, $"Unknown subcommand '{subcommand}'."),
            };
        }
        catch (InvalidOperationException ex)
        {
            // RayNeoClient.Open throws this with a human-readable message when
            // the glasses aren't connected or the interface can't be opened.
            System.Console.Error.WriteLine(ex.Message);
            return ExitDeviceMissing;
        }
    }

    private static int PrintUsage(int exitCode, string? error = null)
    {
        if (error is not null)
        {
            System.Console.Error.WriteLine(error);
        }
        System.Console.WriteLine("Usage: RayNeo.Console [run|calibrate]");
        System.Console.WriteLine("  run        stream live head orientation (default)");
        System.Console.WriteLine("  calibrate  measure tick rate and gyro axis mapping");
        return exitCode;
    }

    // ---- run ----------------------------------------------------------------

    private static int RunLiveReadout()
    {
        using RayNeoClient client = RayNeoClient.Open();
        client.EnableImu();

        // The device's tick counter runs at ~1 MHz, but the exact rate varies.
        // A brief hold-still measurement produces a per-device value so the
        // filter's dt calculation stays honest — otherwise yaw integrates
        // three orders of magnitude too fast.
        float tickRateHz = MeasureTickRate(client, streamSeconds: 2);
        System.Console.WriteLine($"Measured tick rate: {tickRateHz:F0} Hz");

        var filter = new HeadOrientationFilter { TickRateHz = tickRateHz };
        var last = default(RayNeoImuSample);

        client.SampleReceived += sample =>
        {
            if (!sample.IsNewerThan(last))
            {
                return;
            }
            last = sample;
            filter.Update(sample);
        };

        System.Console.WriteLine("Move your head. Press any key to stop.");

        while (!System.Console.KeyAvailable)
        {
            System.Console.Write(
                $"\rpitch {filter.PitchDegrees,7:F2}°  roll {filter.RollDegrees,7:F2}°  " +
                $"yaw {filter.YawDegrees,7:F2}°  temp {last.TemperatureCelsius,5:F1}°C   ");
            Thread.Sleep(50);
        }

        System.Console.WriteLine();
        System.Console.WriteLine("Stopping.");
        return ExitOk;
    }

    // ---- calibrate ----------------------------------------------------------

    private static int RunCalibration()
    {
        using RayNeoClient client = RayNeoClient.Open();
        client.EnableImu();

        float tickRateHz = MeasureTickRate(client, streamSeconds: 10);
        System.Console.WriteLine();
        System.Console.WriteLine($"Measured tick rate: {tickRateHz:F2} Hz");

        System.Console.WriteLine();
        System.Console.WriteLine("Now we will identify which gyro axis corresponds to each motion.");
        System.Console.WriteLine("Each prompt gives you 3 seconds — perform the motion continuously.");

        string nodAxis = SampleMotion(client, "NOD now (chin to chest, up and down)");
        string shakeAxis = SampleMotion(client, "SHAKE now (left/right, saying no)");
        string rollAxis = SampleMotion(client, "ROLL now (tilt ear toward shoulder)");

        System.Console.WriteLine();
        System.Console.WriteLine("Concluded axis-to-motion mapping:");
        System.Console.WriteLine($"  nod   (pitch) -> gyro {nodAxis}");
        System.Console.WriteLine($"  shake (yaw)   -> gyro {shakeAxis}");
        System.Console.WriteLine($"  roll  (roll)  -> gyro {rollAxis}");
        System.Console.WriteLine($"  TickRateHz    = {tickRateHz:F2}");
        return ExitOk;
    }

    /// <summary>
    /// Streams samples for <paramref name="streamSeconds"/> seconds and returns
    /// the device's tick counter rate, measured against wall-clock time.
    /// </summary>
    private static float MeasureTickRate(RayNeoClient client, int streamSeconds)
    {
        System.Console.WriteLine($"Hold the glasses still. Measuring tick rate for {streamSeconds} s…");

        uint firstTick = 0;
        uint lastTick = 0;
        bool haveFirst = false;
        long firstMs = 0;
        var stopwatch = Stopwatch.StartNew();

        void OnSample(RayNeoImuSample s)
        {
            if (!haveFirst)
            {
                firstTick = s.Tick;
                firstMs = stopwatch.ElapsedMilliseconds;
                haveFirst = true;
            }
            lastTick = s.Tick;
        }

        client.SampleReceived += OnSample;
        try
        {
            Thread.Sleep(streamSeconds * 1000);
        }
        finally
        {
            client.SampleReceived -= OnSample;
        }

        long endMs = stopwatch.ElapsedMilliseconds;
        long elapsedMs = endMs - firstMs;
        if (!haveFirst || elapsedMs <= 0)
        {
            System.Console.Error.WriteLine("Warning: no samples arrived during tick-rate measurement.");
            return 1000f;
        }

        uint tickDelta = unchecked(lastTick - firstTick);
        return tickDelta * 1000f / elapsedMs;
    }

    /// <summary>
    /// Prompts the user, records ~3 s of gyro data, and returns "X" / "Y" / "Z"
    /// for the axis with the highest RMS magnitude during that interval.
    /// </summary>
    private static string SampleMotion(RayNeoClient client, string prompt)
    {
        System.Console.WriteLine();
        System.Console.WriteLine(prompt);
        for (int i = 3; i >= 1; i--)
        {
            System.Console.Write($"  starting in {i}… ");
            Thread.Sleep(1000);
        }
        System.Console.WriteLine();
        System.Console.WriteLine("  GO!");

        double sumX = 0, sumY = 0, sumZ = 0;
        int count = 0;
        uint previousTick = 0;
        bool havePrevious = false;

        void OnSample(RayNeoImuSample s)
        {
            // Dedup — the transport repeats samples at ~495 Hz.
            if (havePrevious && s.Tick == previousTick)
            {
                return;
            }
            previousTick = s.Tick;
            havePrevious = true;

            sumX += s.GyroX * s.GyroX;
            sumY += s.GyroY * s.GyroY;
            sumZ += s.GyroZ * s.GyroZ;
            count++;
        }

        client.SampleReceived += OnSample;
        try
        {
            Thread.Sleep(3000);
        }
        finally
        {
            client.SampleReceived -= OnSample;
        }

        if (count == 0)
        {
            System.Console.Error.WriteLine("  Warning: no samples captured for this motion.");
            return "?";
        }

        double rmsX = Math.Sqrt(sumX / count);
        double rmsY = Math.Sqrt(sumY / count);
        double rmsZ = Math.Sqrt(sumZ / count);
        System.Console.WriteLine($"  RMS  X={rmsX:F2}  Y={rmsY:F2}  Z={rmsZ:F2} dps");

        if (rmsX >= rmsY && rmsX >= rmsZ) return "X";
        if (rmsY >= rmsX && rmsY >= rmsZ) return "Y";
        return "Z";
    }
}
