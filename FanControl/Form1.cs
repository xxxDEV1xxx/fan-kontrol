using System;
using System.Drawing;
using System.Management;
using LibreHardwareMonitor.Hardware;
using System.Text.Json;
using System.Windows.Forms;

namespace FanControl;

public partial class Form1 : Form
{
    private readonly CpuPowerController cpuPower = new();
    private readonly System.Windows.Forms.Timer monitor = new();
    private readonly System.Windows.Forms.Timer fanMonitor = new();
    private readonly System.Windows.Forms.Timer fanApplyTimer = new();
    private readonly System.Windows.Forms.Timer cpuApplyTimer = new();
    private readonly NbfcClient nbfc = new();
    private readonly Button applyFan = new();
    private readonly Button autoFan = new();

    private readonly ComboBox profile = new();
    private readonly TrackBar fanSpeed = new();
    private readonly NumericUpDown fanMin = new();
    private readonly NumericUpDown fanMax = new();
    private readonly NumericUpDown fanEmergency = new();

    private readonly TrackBar cpuTarget = new();
    private readonly TrackBar fallbackPercent = new();
    private readonly NumericUpDown maxTemp = new();
    private readonly NumericUpDown recoveryTemp = new();

    private readonly Label fanTargetText = new();
    private readonly Label rpmText = new();
    private readonly Label cpuTempText = new();
    private readonly Label cpuStateText = new();
    private readonly Label hardwareText = new();

    private bool fallbackActive;
    private int? lastAppliedTarget;
    private bool cpuReady;
    private double? lastTemperature;
    private readonly Computer hardwareMonitor = new() { IsCpuEnabled = true };
    private bool hardwareMonitorOpen;
    private readonly Label logicalCpuLoads = new()
    {
        AutoSize = true,
        ForeColor = Color.LightSkyBlue,
        Font = new Font("Consolas", 10F),
        Text = "Waiting for CPU load readings..."
    };


    public Form1()
    {
        Text = ".NET Fan and CPU Control";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(780, 850);
        MinimumSize = new Size(700, 750);
        BackColor = Color.FromArgb(25, 28, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScroll = true;

        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(18),
            BackColor = BackColor
        };
        Controls.Add(root);

        var title = new Label
        {
            Text = "FAN + CPU CONTROL",
            Font = new Font("Segoe UI", 21, FontStyle.Bold),
            ForeColor = Color.LightSkyBlue,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        root.Controls.Add(title);

        var fanGroup = MakeGroup("FAN SETTINGS", 720, 390);
        root.Controls.Add(fanGroup);

        var fan = MakeTable(fanGroup, 8);
        AddRow(fan, "Fan profile", profile);
        profile.DropDownStyle = ComboBoxStyle.DropDownList;
        profile.Items.AddRange(new object[]
        {
            "Silent", "Balanced", "Performance",
            "Maximum Cooling", "Custom"
        });
        profile.SelectedItem = "Balanced";

        AddRow(fan, "Requested fan speed", fanTargetText);
        ConfigureTrack(fanSpeed, 65);
        AddWide(fan, fanSpeed);

        var fanActions = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            BackColor = fanGroup.BackColor,
            Margin = new Padding(2, 5, 2, 5)
        };

        applyFan.Text = "APPLY FAN SPEED";
        applyFan.Width = 180;
        applyFan.Height = 36;
        applyFan.BackColor = Color.FromArgb(35, 115, 190);
        applyFan.ForeColor = Color.White;
        applyFan.FlatStyle = FlatStyle.Flat;
        applyFan.Click += (_, _) => ApplyFanSpeed();

        autoFan.Text = "AUTOMATIC MODE";
        autoFan.Width = 180;
        autoFan.Height = 36;
        autoFan.BackColor = Color.FromArgb(65, 90, 70);
        autoFan.ForeColor = Color.White;
        autoFan.FlatStyle = FlatStyle.Flat;
        autoFan.Click += (_, _) => SetFanAutomatic();

        fanActions.Controls.Add(applyFan);
        fanActions.Controls.Add(autoFan);
        AddWide(fan, fanActions);

        AddRow(fan, "Fan emergency temperature (°C)", fanEmergency);
        ConfigureNumber(fanEmergency, 50, 110, 85);

        AddRow(fan, "Fan feedback (NBFC %)", rpmText);
        rpmText.Text = "Connecting to NBFC...";

        AddRow(fan, "Fan hardware status", hardwareText);
        hardwareText.Text = "NBFC detected; checking service";

        var cpuGroup = MakeGroup("CPU PERFORMANCE + THERMAL FALLBACK", 720, 390);
        root.Controls.Add(cpuGroup);

        var loadGroup = MakeGroup("LIVE LOGICAL PROCESSOR LOAD", 720, 180);
        root.Controls.Add(loadGroup);

        var loadPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
            AutoScroll = true
        };

        loadGroup.Controls.Add(loadPanel);
        loadPanel.Controls.Add(logicalCpuLoads);
        var cpu = MakeTable(cpuGroup, 8);

        AddRow(cpu, "Live CPU temperature", cpuTempText);
        cpuTempText.Text = "Reading sensor...";

        AddRow(cpu, "Normal processor target", MakeValueLabel("100%"));
        ConfigureTrack(cpuTarget, 100);
        AddWide(cpu, cpuTarget);

        AddRow(cpu, "Fallback processor target", MakeValueLabel("50%"));
        ConfigureTrack(fallbackPercent, 50);
        AddWide(cpu, fallbackPercent);

        AddRow(cpu, "Fallback trigger temperature (°C)", maxTemp);
        ConfigureNumber(maxTemp, 60, 110, 90);

        AddRow(cpu, "Recovery temperature (°C)", recoveryTemp);
        ConfigureNumber(recoveryTemp, 30, 100, 75);

        AddRow(cpu, "Thermal controller state", cpuStateText);
        cpuStateText.Text = "Initializing";

        var apply = new Button
        {
            Text = "APPLY CPU SETTINGS",
            Width = 220,
            Height = 42,
            BackColor = Color.FromArgb(35, 115, 190),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 12, 0, 8)
        };
        apply.Click += (_, _) => ApplyCpuTarget();
        root.Controls.Add(apply);

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(700, 0),
            ForeColor = Color.Khaki,
            Text = "Fan speed remains monitoring/configuration only until a supported fan controller is connected. CPU target uses Windows power management, not an exact utilization limit."
        };
        root.Controls.Add(note);

        fanApplyTimer.Interval = 250;
        fanApplyTimer.Tick += (_, _) =>
        {
            fanApplyTimer.Stop();
            ApplyFanSpeed();
        };

        cpuApplyTimer.Interval = 250;
        cpuApplyTimer.Tick += (_, _) =>
        {
            cpuApplyTimer.Stop();
            ApplyCpuTarget();
        };

        fanSpeed.ValueChanged += (_, _) =>
        {
            fanTargetText.Text = $"{fanSpeed.Value}% requested";
            fanApplyTimer.Stop();
            fanApplyTimer.Start();
        };

        profile.SelectedIndexChanged += (_, _) =>
        {
            int? preset = profile.SelectedItem?.ToString() switch
            {
                "Silent" => 35,
                "Balanced" => 60,
                "Performance" => 80,
                "Maximum Cooling" => 100,
                _ => null
            };

            if (preset.HasValue)
                fanSpeed.Value = preset.Value;
        };

        cpuTarget.ValueChanged += (_, _) =>
        {
            ((Label)cpu.Controls[3]).Text = $"{cpuTarget.Value}%";
            cpuApplyTimer.Stop();
            cpuApplyTimer.Start();
        };

        fallbackPercent.ValueChanged += (_, _) =>
        {
            ((Label)cpu.Controls[6]).Text = $"{fallbackPercent.Value}%";
            if (fallbackActive)
            {
                cpuApplyTimer.Stop();
                cpuApplyTimer.Start();
            }
        };

        EventHandler thresholdChanged = (_, _) =>
        {
            if (recoveryTemp.Value < maxTemp.Value &&
                cpuStateText.Text.StartsWith("Invalid thresholds",
                    StringComparison.Ordinal))
            {
                cpuStateText.Text = "Thresholds valid; CPU settings ready.";
            }
        };
        maxTemp.ValueChanged += thresholdChanged;
        recoveryTemp.ValueChanged += thresholdChanged;

        cpuReady = cpuPower.Initialize(out string initStatus);
        cpuStateText.Text = initStatus;

        try
        {
            hardwareMonitor.Open();
            hardwareMonitorOpen = true;
            DumpCpuSensorInventory();
        }
        catch (Exception ex)
        {
            cpuTempText.Text = $"Hardware sensor initialization failed: {ex.Message}";
        }

        monitor.Interval = 1000;
        monitor.Tick += (_, _) => MonitorCpu();
        monitor.Tick += (_, _) => UpdateLogicalProcessorLoads();
        monitor.Start();

        fanMonitor.Interval = 2500;
        fanMonitor.Tick += (_, _) => MonitorFan();
        fanMonitor.Start();
        MonitorFan();

        FormClosing += (_, _) =>
        {
            monitor.Stop();
            fanMonitor.Stop();
            cpuPower.Dispose();
            if (hardwareMonitorOpen)
            {
                hardwareMonitor.Close();
                hardwareMonitorOpen = false;
            }
        };
    }

    private static GroupBox MakeGroup(string text, int width, int height)
        => new()
        {
            Text = text,
            Width = width,
            Height = height,
            ForeColor = Color.LightSkyBlue,
            BackColor = Color.FromArgb(32, 36, 44),
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 14)
        };

    private static TableLayoutPanel MakeTable(Control parent, int rows)
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows,
            BackColor = parent.BackColor,
            AutoSize = false
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        parent.Controls.Add(table);
        return table;
    }

    private static Label MakeValueLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.LightSkyBlue
    };

    private static void AddRow(
        TableLayoutPanel table, string caption, Control value)
    {
        int row = table.Controls.Count == 0
            ? 0
            : table.GetRow(table.Controls[table.Controls.Count - 1]) + 1;

        table.RowCount = Math.Max(table.RowCount, row + 1);
        table.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = Color.White,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 9, 2, 9)
        }, 0, row);

        value.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        value.Margin = new Padding(2, 6, 2, 6);
        table.Controls.Add(value, 1, row);
    }

    private static void AddWide(TableLayoutPanel table, Control control)
    {
        int row = table.GetRow(table.Controls[table.Controls.Count - 1]) + 1;
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }

    private static void ConfigureTrack(TrackBar track, int initial)
    {
        track.Minimum = 1;
        track.Maximum = 100;
        track.Value = initial;
        track.TickFrequency = 10;
        track.Dock = DockStyle.Fill;
        track.BackColor = Color.FromArgb(32, 36, 44);
    }

    private static void ConfigureNumber(
        NumericUpDown control, int min, int max, int value)
    {
        control.Minimum = min;
        control.Maximum = max;
        control.Value = value;
        control.Width = 110;
        control.BackColor = Color.FromArgb(40, 44, 52);
        control.ForeColor = Color.White;
    }

    private void ApplyFanSpeed()
    {
        int requested = fanSpeed.Value;
        int target = fallbackActive ? 100 : requested;
        fanTargetText.Text = $"{requested}% requested";

        if (nbfc.TrySetSpeed(target, out string status))
        {
            hardwareText.Text = fallbackActive
                ? $"THERMAL OVERRIDE: fan command sent: {target}%"
                : $"NBFC command sent: {target}%";
            MonitorFan();
        }
        else
        {
            hardwareText.Text = $"Fan command failed: {status}";
        }
    }

    private void SetFanAutomatic()
    {
        fanApplyTimer.Stop();

        if (fallbackActive)
        {
            ApplyFanSpeed();
            return;
        }
        if (nbfc.TrySetAutomatic(out string status))
        {
            hardwareText.Text = "NBFC automatic mode requested";
            MonitorFan();
        }
        else
        {
            hardwareText.Text = $"Automatic mode failed: {status}";
        }
    }

    private void MonitorFan()
    {
        if (!nbfc.TryGetStatus(out NbfcSnapshot? state, out string error) ||
            state is null)
        {
            hardwareText.Text = $"NBFC status error: {error}";
            rpmText.Text = "No valid fan feedback";
            return;
        }

        if (!state.ServiceEnabled)
        {
            hardwareText.Text = "NBFC service disabled";
            rpmText.Text = "Fan status unavailable";
            return;
        }

        string current = state.CurrentSpeedPercent.HasValue
            ? $"{state.CurrentSpeedPercent.Value:F0}%"
            : "not reported";

        string target = state.TargetSpeedPercent.HasValue
            ? $"{state.TargetSpeedPercent.Value:F0}%"
            : "not reported";

        rpmText.Text = $"Current: {current} | Target: {target}";

        string temperature = state.TemperatureC.HasValue
            ? $"{state.TemperatureC.Value:F0} °C"
            : "temperature not reported";

        string automatic = state.AutoControl.HasValue
            ? (state.AutoControl.Value ? "Auto ON" : "Manual mode")
            : "Auto state unknown";

        string critical = state.CriticalMode.HasValue
            ? (state.CriticalMode.Value ? "Critical ON" : "Critical OFF")
            : "Critical state unknown";

        hardwareText.Text =
            $"{state.Config} | {temperature} | {automatic} | {critical}";
    }

    private void DumpCpuSensorInventory()
    {
        var lines = new System.Collections.Generic.List<string>();
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "FanControl-cpu-sensors.txt");

        try
        {
            void Scan(IHardware hardware)
            {
                hardware.Update();
                lines.Add($"HARDWARE: {hardware.Name} [{hardware.HardwareType}]");

                foreach (ISensor sensor in hardware.Sensors)
                {
                    string value = sensor.Value.HasValue
                        ? sensor.Value.Value.ToString("F2")
                        : "null";

                    lines.Add(
                        $"  {sensor.SensorType} | {sensor.Name} | {value}");
                }

                foreach (IHardware child in hardware.SubHardware)
                    Scan(child);
            }

            foreach (IHardware hardware in hardwareMonitor.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                    Scan(hardware);
            }

            if (lines.Count == 0)
                lines.Add("No CPU hardware exposed by LibreHardwareMonitor.");

            System.IO.File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            lines.Add("DIAGNOSTIC ERROR: " + ex);
            System.IO.File.WriteAllLines(path, lines);
        }
    }
    private void UpdateLogicalProcessorLoads()
    {
        try
        {
            var readings = new System.Collections.Generic.List<string>();

            void Scan(IHardware hardware)
            {
                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Load)
                        continue;

                    if (!sensor.Value.HasValue)
                        continue;

                    if (sensor.Name.StartsWith("CPU Core #",
                        StringComparison.OrdinalIgnoreCase) &&
                        sensor.Name.Contains("Thread #",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        readings.Add(
                            $"{sensor.Name}: {sensor.Value.Value:F1}%");
                    }
                    else if (sensor.Name.Equals("CPU Total",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        readings.Add(
                            $"TOTAL CPU LOAD: {sensor.Value.Value:F1}%");
                    }
                }

                foreach (IHardware child in hardware.SubHardware)
                    Scan(child);
            }

            foreach (IHardware hardware in hardwareMonitor.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                    Scan(hardware);
            }

            logicalCpuLoads.Text = readings.Count > 0
                ? string.Join(Environment.NewLine, readings)
                : "Logical processor load sensors unavailable.";
        }
        catch (Exception ex)
        {
            logicalCpuLoads.Text = "CPU load read error: " + ex.Message;
        }
    }
    private bool TryReadLiveCpuTemperature(
        out double temperature,
        out string sensorName)
    {
        temperature = 0;
        sensorName = "";

        try
        {
            var readings = new System.Collections.Generic.List<
                (double Value, string Name)>();

            void ReadHardware(IHardware hardware)
            {
                hardware.Update();

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature ||
                        !sensor.Value.HasValue)
                        continue;

                    double value = sensor.Value.Value;

                    if (value < 15 || value > 120)
                        continue;

                    readings.Add((value, sensor.Name));
                }

                foreach (IHardware child in hardware.SubHardware)
                    ReadHardware(child);
            }

            foreach (IHardware hardware in hardwareMonitor.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                    ReadHardware(hardware);
            }

            if (readings.Count == 0)
                return false;

            var preferred = readings.Where(x =>
                x.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("Core Max", StringComparison.OrdinalIgnoreCase));

            var selected = preferred.Any()
                ? preferred.OrderByDescending(x => x.Value).First()
                : readings.OrderByDescending(x => x.Value).First();

            temperature = selected.Value;
            sensorName = selected.Name;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void MonitorCpu()
    {
        try
        {
            bool liveReading = TryReadLiveCpuTemperature(
                out double liveC,
                out string sensorName);

            double? cpuC = liveReading ? liveC : null;

            if (liveReading)
            {
                cpuTempText.Text =
                    $"{liveC:F1} °C (LibreHardwareMonitor: {sensorName})";
            }
            else
            {
                // NBFC reports the configured fan-control thermal sensor.
                // Its physical location is not yet verified as CPU package/core.
                if (nbfc.TryGetStatus(
                    out NbfcSnapshot? nbfcState,
                    out string nbfcError) &&
                    nbfcState is not null &&
                    nbfcState.ServiceEnabled &&
                    nbfcState.TemperatureC.HasValue &&
                    nbfcState.TemperatureC.Value >= 15 &&
                    nbfcState.TemperatureC.Value <= 120)
                {
                    cpuTempText.Text =
                        $"{nbfcState.TemperatureC.Value:F1} °C (NBFC thermal sensor)";
                }
                else
                {
                    cpuTempText.Text = "Live thermal sensor unavailable";
                }

                cpuStateText.Text = fallbackActive
                    ? "CPU temperature unavailable; thermal override remains active."
                    : "CPU temperature unavailable; thermal automation paused.";

                // Do not release an existing override from an unverified NBFC reading.
                // Keep applying the CPU fallback target if it is active.
                if (fallbackActive)
                    ApplyCpuTarget();

                return;
            }

            lastTemperature = cpuC;

            if (!fallbackActive && liveC >= (double)maxTemp.Value)
            {
                fallbackActive = true;
                lastAppliedTarget = null;
                ApplyFanSpeed();
            }
            else if (fallbackActive &&
                     liveC <= (double)recoveryTemp.Value)
            {
                fallbackActive = false;
                lastAppliedTarget = null;
                ApplyFanSpeed();
            }

            ApplyCpuTarget();
        }
        catch (Exception ex)
        {
            cpuTempText.Text = "Sensor read error";
            cpuStateText.Text = ex.Message;
        }
    }

    private void ApplyCpuTarget()
    {

        if (decimal.Compare(recoveryTemp.Value, maxTemp.Value) >= 0)
        {
            cpuStateText.Text =
                "Invalid thresholds: recovery must be below trigger.";
            return;
        }

        if (!cpuReady)
        {
            cpuStateText.Text =
                "Power controller unavailable; no CPU setting applied.";
            return;
        }

        int target = fallbackActive
            ? (int)fallbackPercent.Value
            : (int)cpuTarget.Value;

        if (lastAppliedTarget == target)
            return;

        if (cpuPower.ApplyPercent(target, out string status))
        {
            lastAppliedTarget = target;
            cpuStateText.Text = fallbackActive
                ? $"THERMAL FALLBACK: {status}"
                : $"Normal mode: {status}";
        }
        else
        {
            cpuStateText.Text = status;
        }
    }
}
