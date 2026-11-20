// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved

namespace BridgingIT.DevKit.Application.Benchmarks;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class StorageDimensionsQualificationBudget
{
    private static IntPtr job;

    // The coordinator and its child share four logical CPUs and an 8-GiB hard job limit.
    // The owned database container receives four CPU equivalents and 24 GiB separately.
    public static int? StartBudgetedChild(string[] args)
    {
        if (args.Contains("--dimensions-budgeted-child")) return null;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This launcher implements the approved local Windows resource budget only.");
        var engineIndex = Array.IndexOf(args, "--engine");
        var sqlite = engineIndex < 0 || args[engineIndex + 1] == "sqlite";
        job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero) throw new Win32Exception();
        var limits = new ExtendedLimitInformation { Basic = new() { LimitFlags = 0x200 }, JobMemoryLimit = new UIntPtr((ulong)(sqlite ? 32L : 8L) * 1024 * 1024 * 1024) };
        var size = Marshal.SizeOf<ExtendedLimitInformation>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(limits, pointer, false);
            if (!SetInformationJobObject(job, 9, pointer, (uint)size) || !AssignProcessToJobObject(job, Process.GetCurrentProcess().Handle)) throw new Win32Exception();
        }
        finally { Marshal.FreeHGlobal(pointer); }

        using var current = Process.GetCurrentProcess();
        current.ProcessorAffinity = (IntPtr)(sqlite ? 255 : 15);
        var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(StorageDimensionsQualificationBudget).Assembly.Location);
        foreach (var argument in args) start.ArgumentList.Add(argument);
        start.ArgumentList.Add("--dimensions-budgeted-child");
        start.Environment["DOTNET_GCHeapHardLimit"] = sqlite ? "600000000" : "100000000"; // Hexadecimal 24/4 GiB, within the job budget.
        start.Environment["DOTNET_PROCESSOR_COUNT"] = sqlite ? "8" : "4";
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Could not start budgeted workload.");
        child.WaitForExit();
        return child.ExitCode;
    }

    public static object CaptureEffectiveBudget(string engine, bool requireMatch)
    {
        if (!OperatingSystem.IsWindows())
        {
            if (requireMatch) throw new PlatformNotSupportedException("No enforced budget is available on this platform.");
            return new { Verified = false, Reason = "No Windows job budget" };
        }

        var size = Marshal.SizeOf<ExtendedLimitInformation>();
        var pointer = Marshal.AllocHGlobal(size);
        try
        {
            // NULL selects the calling process's immediate job, including inherited membership.
            if (!QueryInformationJobObject(IntPtr.Zero, 9, pointer, (uint)size, out _))
            {
                if (requireMatch) throw new Win32Exception();
                return new { Verified = false, Reason = "No queryable job" };
            }

            var limits = Marshal.PtrToStructure<ExtendedLimitInformation>(pointer);
            using var process = Process.GetCurrentProcess();
            var affinity = process.ProcessorAffinity.ToInt64();
            var expectedBytes = (engine == "sqlite" ? 32UL : 8UL) * 1024 * 1024 * 1024;
            var matches = (limits.Basic.LimitFlags & 0x200) != 0 && limits.JobMemoryLimit.ToUInt64() == expectedBytes
                && affinity == (engine == "sqlite" ? 255 : 15);
            if (requireMatch && !matches) throw new InvalidOperationException("Effective application job limits differ from the reference budget.");
            return new
            {
                Verified = matches, JobMemoryLimitBytes = limits.JobMemoryLimit.ToUInt64(),
                AffinityMask = affinity, PeakJobCommittedBytes = limits.PeakJobMemoryUsed.ToUInt64(),
                ProcessWorkingSetBytes = process.WorkingSet64,
                ServerGC = System.Runtime.GCSettings.IsServerGC,
                ProcessorCount = Environment.ProcessorCount,
                PhysicalHost = CapturePhysicalMemory(),
                Note = "The job caps process commitment. Windows filesystem cache belongs to the physical host and is reported separately; this is not a VM memory-isolation claim."
            };
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private static object CapturePhysicalMemory()
    {
        if (!GetPerformanceInfo(out var info, (uint)Marshal.SizeOf<PerformanceInformation>())) throw new Win32Exception();
        var pageBytes = info.PageSize.ToUInt64();
        return new
        {
            TotalBytes = info.PhysicalTotal.ToUInt64() * pageBytes,
            AvailableBytes = info.PhysicalAvailable.ToUInt64() * pageBytes,
            SystemCacheAndStandbyBytes = info.SystemCache.ToUInt64() * pageBytes,
            CommittedBytes = info.CommitTotal.ToUInt64() * pageBytes,
            Note = "Whole physical host, including unrelated applications. SystemCache is standby plus system working set; it is not the benchmark database's private cache."
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache;
        public UIntPtr KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation information, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(IntPtr handle, int informationClass, IntPtr information, uint length, out uint returnedLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr handle, int informationClass, IntPtr information, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr handle, IntPtr process);
}
