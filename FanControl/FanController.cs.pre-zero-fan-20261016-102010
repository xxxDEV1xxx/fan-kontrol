using System;
using System.Collections.Generic;
using System.Linq;

namespace FanControl;

public enum FanProfile
{
    Silent,
    Balanced,
    Performance,
    MaximumCooling,
    Custom
}

public sealed class FanReading
{
    public string Name { get; init; } = "Fan";
    public int? Rpm { get; init; }
    public double? TemperatureC { get; init; }
    public int? RequestedPercent { get; init; }
    public int? ActualPercent { get; init; }
    public bool ControlSupported { get; init; }
    public string Status { get; init; } = "Not initialized";
}

public sealed class FanCurvePoint
{
    public double TemperatureC { get; init; }
    public int Percent { get; init; }
}

public interface IFanHardware
{
    IReadOnlyList<FanReading> ReadFans();

    bool TrySetFanPercent(
        string fanName,
        int percent,
        out string status);
}

public sealed class FanController
{
    private readonly IFanHardware hardware;

    public FanProfile Profile { get; set; } = FanProfile.Balanced;

    public int MinimumPercent { get; set; } = 25;
    public int MaximumPercent { get; set; } = 100;
    public double EmergencyTemperatureC { get; set; } = 85;

    public List<FanCurvePoint> Curve { get; } = new()
    {
        new() { TemperatureC = 30, Percent = 25 },
        new() { TemperatureC = 40, Percent = 35 },
        new() { TemperatureC = 50, Percent = 50 },
        new() { TemperatureC = 60, Percent = 65 },
        new() { TemperatureC = 70, Percent = 80 },
        new() { TemperatureC = 80, Percent = 100 }
    };

    public FanController(IFanHardware hardware)
    {
        this.hardware = hardware;
    }

    public IReadOnlyList<FanReading> ReadFans()
        => hardware.ReadFans();

    public bool SetManualSpeed(
        string fanName,
        int percent,
        out string status)
    {
        percent = Math.Clamp(percent, 0, 100);

        if (percent > 0 && percent < MinimumPercent)
            percent = MinimumPercent;

        percent = Math.Min(percent, MaximumPercent);

        return hardware.TrySetFanPercent(
            fanName, percent, out status);
    }

    public int CalculateTarget(double temperatureC)
    {
        if (temperatureC >= EmergencyTemperatureC)
            return 100;

        var curve = Profile switch
        {
            FanProfile.Silent => new[]
            {
                (30.0, 20), (45.0, 30),
                (60.0, 45), (75.0, 75), (85.0, 100)
            },
            FanProfile.Balanced => new[]
            {
                (30.0, 25), (45.0, 40),
                (60.0, 60), (75.0, 85), (85.0, 100)
            },
            FanProfile.Performance => new[]
            {
                (30.0, 40), (45.0, 60),
                (60.0, 80), (70.0, 100)
            },
            FanProfile.MaximumCooling => new[]
            {
                (0.0, 100), (100.0, 100)
            },
            _ => Curve
                .OrderBy(p => p.TemperatureC)
                .Select(p => (p.TemperatureC, p.Percent))
                .ToArray()
        };

        var points = curve.OrderBy(p => p.Item1).ToArray();

        if (temperatureC <= points[0].Item1)
            return Math.Clamp(points[0].Item2,
                MinimumPercent, MaximumPercent);

        for (int i = 1; i < points.Length; i++)
        {
            var (t1, s1) = points[i - 1];
            var (t2, s2) = points[i];

            if (temperatureC <= t2)
            {
                double fraction = (temperatureC - t1) / (t2 - t1);
                int speed = (int)Math.Round(s1 + fraction * (s2 - s1));

                return Math.Clamp(speed, MinimumPercent, MaximumPercent);
            }
        }

        return MaximumPercent;
    }

    public bool ApplyAutomaticSpeed(
        string fanName,
        double temperatureC,
        out string status)
    {
        int target = CalculateTarget(temperatureC);
        return hardware.TrySetFanPercent(fanName, target, out status);
    }
}
