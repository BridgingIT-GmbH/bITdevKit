// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static partial class StorageDimensionsQualification
{
    private static async Task<object> CaptureTimerPrecisionAsync(bool requested)
    {
        var samples = new double[100];
        for (var index = 0; index < samples.Length; index++)
        {
            var started = Stopwatch.GetTimestamp();
            await Task.Delay(1).ConfigureAwait(false);
            samples[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(samples);
        return new
        {
            RequestedPeriodMilliseconds = requested ? (int?)1 : null,
            PowerThrottling = QualificationTimerResolution.CapturePowerThrottling(),
            ProbeRequestedDelayMilliseconds = 1,
            ProbeMinimumMilliseconds = samples[0],
            ProbeMedianMilliseconds = samples[samples.Length / 2],
            ProbeP95Milliseconds = samples[(int)(samples.Length * 0.95)],
            ProbeMaximumMilliseconds = samples[^1],
            Note = "Probe outside measured work. The Windows timer request and policy belong only to the benchmark process and are restored on disposal; request rates and queue deadlines are unchanged."
        };
    }

    private sealed class QualificationTimerResolution : IDisposable
    {
        private const int ProcessPowerThrottling = 4;
        private const uint IgnoreTimerResolution = 4;
        private readonly PowerThrottlingState? originalPolicy;
        private bool disposed;

        public QualificationTimerResolution()
        {
            if (!OperatingSystem.IsWindows()) return;
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                this.originalPolicy = ReadPowerThrottling();
                var policy = this.originalPolicy.Value;
                policy.ControlMask |= IgnoreTimerResolution;
                policy.StateMask &= ~IgnoreTimerResolution;
                SetPowerThrottling(policy);
            }

            if (BeginPeriod(1) != 0)
            {
                if (this.originalPolicy.HasValue) SetPowerThrottling(this.originalPolicy.Value);
                throw new InvalidOperationException("The benchmark could not request millisecond timer precision.");
            }

            this.Requested = true;
        }

        public bool Requested { get; }

        public static object CapturePowerThrottling()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return null;
            var policy = ReadPowerThrottling();
            return new
            {
                policy.Version, policy.ControlMask, policy.StateMask,
                TimerRequestsExplicitlyHonored = (policy.ControlMask & IgnoreTimerResolution) != 0 && (policy.StateMask & IgnoreTimerResolution) == 0
            };
        }

        public void Dispose()
        {
            if (this.disposed) return;
            this.disposed = true;
            try
            {
                if (this.originalPolicy.HasValue) SetPowerThrottling(this.originalPolicy.Value);
            }
            finally
            {
                if (this.Requested) EndPeriod(1);
            }
        }

        private static PowerThrottlingState ReadPowerThrottling()
        {
            var state = new PowerThrottlingState { Version = 1 };
            if (!GetProcessInformation(new IntPtr(-1), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>()))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot read benchmark timer policy.");
            return state;
        }

        private static void SetPowerThrottling(PowerThrottlingState state)
        {
            if (!SetProcessInformation(new IntPtr(-1), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>()))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot set benchmark timer policy.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint BeginPeriod(uint period);

        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint EndPeriod(uint period);
    }
}
