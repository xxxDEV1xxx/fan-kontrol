using System;
using System.Runtime.InteropServices;

namespace FanControl;

public sealed class CpuPowerController : IDisposable
{
    private static readonly Guid ProcessorGroup =
        new("54533251-82be-4824-96c1-47b60b740d00");

    private static readonly Guid MaximumState =
        new("bc5038f7-23e0-4960-96da-33abaf5935ec");

    private Guid scheme;
    private uint originalAC;
    private uint originalDC;
    private bool initialized;
    private bool changed;

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(
        IntPtr root, out IntPtr schemePointer);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(
        IntPtr root, ref Guid scheme, ref Guid subgroup,
        ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(
        IntPtr root, ref Guid scheme, ref Guid subgroup,
        ref Guid setting, out uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteACValueIndex(
        IntPtr root, ref Guid scheme, ref Guid subgroup,
        ref Guid setting, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerWriteDCValueIndex(
        IntPtr root, ref Guid scheme, ref Guid subgroup,
        ref Guid setting, uint value);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(
        IntPtr root, ref Guid scheme);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public bool Initialize(out string status)
    {
        IntPtr pointer = IntPtr.Zero;

        try
        {
            uint rc = PowerGetActiveScheme(IntPtr.Zero, out pointer);
            if (rc != 0 || pointer == IntPtr.Zero)
            {
                status = $"Cannot read active power plan (error {rc}).";
                return false;
            }

            scheme = Marshal.PtrToStructure<Guid>(pointer);
            LocalFree(pointer);
            pointer = IntPtr.Zero;

            Guid group = ProcessorGroup;
            Guid setting = MaximumState;

            rc = PowerReadACValueIndex(
                IntPtr.Zero, ref scheme, ref group,
                ref setting, out originalAC);

            if (rc != 0)
            {
                status = $"Cannot read AC processor limit ({rc}).";
                return false;
            }

            rc = PowerReadDCValueIndex(
                IntPtr.Zero, ref scheme, ref group,
                ref setting, out originalDC);

            if (rc != 0)
            {
                status = $"Cannot read battery processor limit ({rc}).";
                return false;
            }

            initialized = true;
            status = $"Windows power controller ready. Original limits: AC {originalAC}%, battery {originalDC}%.";
            return true;
        }
        catch (Exception ex)
        {
            status = ex.Message;
            return false;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
                LocalFree(pointer);
        }
    }

    public bool ApplyPercent(int percent, out string status)
    {
        if (!initialized)
        {
            status = "CPU power controller is not initialized.";
            return false;
        }

        if (percent is < 1 or > 100)
        {
            status = "Processor limit must be between 1 and 100%.";
            return false;
        }

        Guid group = ProcessorGroup;
        Guid setting = MaximumState;
        uint value = (uint)percent;

        uint rc = PowerWriteACValueIndex(
            IntPtr.Zero, ref scheme, ref group, ref setting, value);

        if (rc != 0)
        {
            status = $"Failed to set AC processor limit ({rc}).";
            return false;
        }

        rc = PowerWriteDCValueIndex(
            IntPtr.Zero, ref scheme, ref group, ref setting, value);

        if (rc != 0)
        {
            status = $"Failed to set battery processor limit ({rc}).";
            return false;
        }

        rc = PowerSetActiveScheme(IntPtr.Zero, ref scheme);

        if (rc != 0)
        {
            status = $"Failed to activate updated power plan ({rc}).";
            return false;
        }

        changed = true;
        status = $"Windows processor maximum set to {percent}%.";
        return true;
    }

    public void Dispose()
    {
        if (!initialized || !changed)
            return;

        Guid group = ProcessorGroup;
        Guid setting = MaximumState;

        PowerWriteACValueIndex(
            IntPtr.Zero, ref scheme, ref group, ref setting, originalAC);

        PowerWriteDCValueIndex(
            IntPtr.Zero, ref scheme, ref group, ref setting, originalDC);

        PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        changed = false;
    }
}
