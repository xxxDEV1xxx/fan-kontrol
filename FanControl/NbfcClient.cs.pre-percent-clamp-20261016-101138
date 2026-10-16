using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FanControl;

public sealed record NbfcSnapshot(
    bool ServiceEnabled,
    string Config,
    double? TemperatureC,
    double? CurrentSpeedPercent,
    double? TargetSpeedPercent,
    bool? AutoControl,
    bool? CriticalMode);

public sealed class NbfcClient
{
    private const string ExePath =
        @"C:\Program Files (x86)\NoteBook FanControl\nbfc.exe";

    private static bool Run(
        string arguments,
        out string output,
        out string error)
    {
        output = "";
        error = "";

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!process.Start())
            {
                error = "Could not start nbfc.exe.";
                return false;
            }

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(true); } catch { }
                error = "NBFC command timed out.";
                return false;
            }

            Task.WaitAll(stdout, stderr);
            output = stdout.Result.Trim();
            string errText = stderr.Result.Trim();

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(errText)
                    ? (string.IsNullOrWhiteSpace(output)
                        ? $"NBFC exited with code {process.ExitCode}."
                        : output)
                    : errText;
                return false;
            }

            error = errText;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static double? Number(string text, string pattern)
    {
        Match match = Regex.Match(
            text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return double.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out double value)
            ? value
            : null;
    }

    private static bool? Boolean(string text, string pattern)
    {
        Match match = Regex.Match(
            text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        return bool.TryParse(match.Groups[1].Value, out bool value)
            ? value
            : null;
    }

    public bool TryGetStatus(
        out NbfcSnapshot? snapshot,
        out string error)
    {
        snapshot = null;

        if (!Run("status --all", out string output, out error))
            return false;

        string config = Regex.Match(
            output,
            @"Selected config name\s*:\s*(.*)",
            RegexOptions.IgnoreCase).Groups[1].Value.Trim();

        snapshot = new NbfcSnapshot(
            Boolean(output, @"Service enabled\s*:\s*(True|False)") == true,
            config,
            Number(output, @"Temperature\s*:\s*([0-9]+(?:\.[0-9]+)?)"),
            Number(output, @"Current fan speed\s*:\s*([0-9]+(?:\.[0-9]+)?)"),
            Number(output, @"Target fan speed\s*:\s*([0-9]+(?:\.[0-9]+)?)"),
            Boolean(output, @"Auto control enabled\s*:\s*(True|False)"),
            Boolean(output, @"Critical mode enabled\s*:\s*(True|False)"));

        return true;
    }

    public bool TrySetSpeed(int percent, out string status)
    {
        if (percent < 25 || percent > 100)
        {
            status = "Manual speed is restricted to 25–100%.";
            return false;
        }

        bool ok = Run(
            $"set --speed {percent}",
            out string output,
            out string error);

        status = ok
            ? (string.IsNullOrWhiteSpace(output)
                ? $"NBFC accepted the {percent}% command."
                : output)
            : error;

        return ok;
    }

    public bool TrySetAutomatic(out string status)
    {
        bool ok = Run("set --auto", out string output, out string error);

        status = ok
            ? (string.IsNullOrWhiteSpace(output)
                ? "NBFC automatic mode command accepted."
                : output)
            : error;

        return ok;
    }
}
