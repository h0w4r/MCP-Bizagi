using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace McpBizagi.Server;

/// <summary>Windows closes the entire owned worker job if the MCP host exits unexpectedly.</summary>
internal sealed partial class WorkerJob : IDisposable
{
    private readonly SafeFileHandle handle;
    public WorkerJob()
    {
        handle = new SafeFileHandle(CreateJobObjectW(0, 0), ownsHandle: true);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        if (!SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<ExtendedLimits>()))
        { int error = Marshal.GetLastPInvokeError(); handle.Dispose(); throw new Win32Exception(error); }
    }
    public void Assign(Process process)
    {
        if (!AssignProcessToJobObject(handle, process.SafeHandle)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public void Dispose() => handle.Dispose();

    // Live owners use this only after their editor has already exited, so cleanup can be observed before disposing the handle.
    public void TerminateRemaining()
    {
        if (!TerminateJobObject(handle, 0)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    public int[] ProcessIds()
    {
        int capacity = 32;
        while (true)
        {
            int bytes = checked(8 + capacity * IntPtr.Size);
            nint buffer = Marshal.AllocHGlobal(bytes);
            try
            {
                if (!QueryInformationJobObject(handle, 3, buffer, (uint)bytes, out _))
                {
                    int error = Marshal.GetLastPInvokeError();
                    if (error == 234) { capacity = checked(capacity * 2); continue; } // ERROR_MORE_DATA: job grew during sampling.
                    throw new Win32Exception(error);
                }
                int count = Marshal.ReadInt32(buffer, 4);
                return Enumerable.Range(0, count).Select(i => checked((int)Marshal.ReadIntPtr(buffer, 8 + i * IntPtr.Size))).ToArray();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
    public (long CpuTicks, ulong IoBytes) Activity()
    {
        int bytes = Marshal.SizeOf<JobAccounting>();
        nint buffer = Marshal.AllocHGlobal(bytes);
        try
        {
            if (!QueryInformationJobObject(handle, 8, buffer, (uint)bytes, out _)) throw new Win32Exception(Marshal.GetLastPInvokeError());
            var value = Marshal.PtrToStructure<JobAccounting>(buffer);
            return (value.UserTime + value.KernelTime, value.Io.ReadTransferCount + value.Io.WriteTransferCount + value.Io.OtherTransferCount);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobAccounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        public IoCounters Io;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize, MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
    private static partial nint CreateJobObjectW(nint attributes, nint name);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateJobObject(SafeFileHandle job, uint exitCode);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetInformationJobObject(SafeFileHandle job, int informationClass, ref ExtendedLimits information, uint length);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AssignProcessToJobObject(SafeFileHandle job, SafeProcessHandle process);
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryInformationJobObject(SafeFileHandle job, int informationClass, nint information, uint length, out uint returnedLength);
}
