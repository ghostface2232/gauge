using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Gauge.Providers.Internal;

/// <summary>
/// Launches a process suspended via <c>CreateProcessW</c> so the caller can place it under a
/// <see cref="JobObject"/> before it runs a single instruction. This closes the race the
/// language server would otherwise open: it spawns sidecar children during startup, and a
/// process started normally could fork one in the window before it is assigned to the job,
/// leaking it past cleanup. Suspended-then-assign-then-resume guarantees every descendant is
/// born inside the job.
/// </summary>
internal sealed class SuspendedProcess : IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint StillActive = 259;

    private IntPtr _processHandle;
    private IntPtr _threadHandle;
    private bool _resumed;

    private SuspendedProcess(int processId, IntPtr processHandle, IntPtr threadHandle)
    {
        ProcessId = processId;
        _processHandle = processHandle;
        _threadHandle = threadHandle;
    }

    public int ProcessId { get; }

    public IntPtr Handle => _processHandle;

    public bool IsAlive => GetExitCodeProcess(_processHandle, out var code) && code == StillActive;

    public static SuspendedProcess Create(
        string executablePath, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        var commandLine = WindowsCommandLine.Join(executablePath, arguments);
        var startupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };

        // CreateProcessW may write to the command-line buffer, so pass a mutable native copy.
        var commandLinePtr = Marshal.StringToHGlobalUni(commandLine);
        try
        {
            var created = CreateProcessW(
                executablePath,
                commandLinePtr,
                IntPtr.Zero,
                IntPtr.Zero,
                bInheritHandles: false,
                CreateSuspended | CreateNoWindow | CreateUnicodeEnvironment,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out var processInformation);

            if (!created)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");
            }

            return new SuspendedProcess(
                processInformation.dwProcessId,
                processInformation.hProcess,
                processInformation.hThread);
        }
        finally
        {
            Marshal.FreeHGlobal(commandLinePtr);
        }
    }

    /// <summary>Starts the suspended primary thread. Call only after assigning to a job.</summary>
    public void Resume()
    {
        if (_threadHandle != IntPtr.Zero)
        {
            ResumeThread(_threadHandle);
            CloseHandle(_threadHandle);
            _threadHandle = IntPtr.Zero;
            _resumed = true;
        }
    }

    public void Dispose()
    {
        // A process disposed before it was resumed never got into a job and would otherwise sit
        // suspended forever once its handles close, so terminate it explicitly. After resume the
        // owning job is responsible for the kill, so we only release our handles here.
        if (!_resumed && _processHandle != IntPtr.Zero && IsAlive)
        {
            TerminateProcess(_processHandle, 1);
        }

        if (_threadHandle != IntPtr.Zero)
        {
            CloseHandle(_threadHandle);
            _threadHandle = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        IntPtr lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
