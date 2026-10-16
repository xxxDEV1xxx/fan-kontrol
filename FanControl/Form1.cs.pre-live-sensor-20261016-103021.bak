using System;
using System.Drawing;
using System.Management;
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

        monitor.Interval = 1000;
        monitor.Tick += (_, _) => MonitorCpu();
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

    private void MonitorCpu()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\wmi",
                "SELECT InstanceName, CurrentTemperature " +
                "FROM MSAcpi_ThermalZoneTemperature");

            double? cpuC = null;

            foreach (ManagementObject item in searcher.Get())
            {
                string name = item["InstanceName"]?.ToString() ?? "";

                if (name.Contains("CPUZ", StringComparison.OrdinalIgnoreCase))
                {
                    double raw = Convert.ToDouble(item["CurrentTemperature"]);
                    double value = raw / 10.0 - 273.15;

                    if (value >= 10 && value <= 125)
                        cpuC = value;

                    break;
                }
            }

            if (cpuC is null)
            {
                cpuTempText.Text = "CPU ACPI sensor unavailable";
                return;
            }

            lastTemperature = cpuC;
            cpuTempText.Text = $"{cpuC:F1} °C (ACPI zone)";

            if (!cpuReady)
            {
                cpuStateText.Text = "Windows CPU power control unavailable";
                return;
            }

            if (!fallbackActive && cpuC.Value >= (double)maxTemp.Value)
            {
                fallbackActive = true;
                lastAppliedTarget = null;
                ApplyFanSpeed();
            
            }
            else if (fallbackActive &&
                     cpuC.Value <= (double)recoveryTemp.Value)
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
