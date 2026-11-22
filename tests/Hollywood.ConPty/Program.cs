using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("ConPTY validation requires Windows 10 build 17763 or newer.");
    return 2;
}

var executable = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/win-x64/hollywood.exe");
if (!File.Exists(executable))
{
    Console.Error.WriteLine($"Executable not found: {executable}");
    return 2;
}

try
{
    using var session = new PseudoConsoleSession(executable, new PseudoConsoleSession.Coord(80, 24));
    await session.StartAsync();
    var sceneDeadline = Stopwatch.StartNew();
    while (!session.CapturedText.Contains("HOLLYWOOD", StringComparison.Ordinal) && sceneDeadline.Elapsed < TimeSpan.FromSeconds(5))
    {
        if (session.HasExited) throw new InvalidOperationException($"Application exited before drawing a scene (exit {session.ExitCode}): {session.CapturedText}");
        await Task.Delay(50);
    }
    if (!session.CapturedText.Contains("HOLLYWOOD", StringComparison.Ordinal)) throw new TimeoutException("No Hollywood frame appeared within five seconds.");

    var firstFrameBytes = session.CapturedByteCount;
    await Task.Delay(700);
    var animatedBytes = session.CapturedByteCount;
    if (animatedBytes <= firstFrameBytes) throw new InvalidOperationException("No terminal output arrived after the first frame; animation did not progress.");

    session.Resize(new PseudoConsoleSession.Coord(42, 12));
    await Task.Delay(500);
    session.Resize(new PseudoConsoleSession.Coord(12, 5));
    await Task.Delay(1000);
    if (!session.CapturedText.Contains("RESIZE", StringComparison.Ordinal))
        throw new InvalidOperationException($"Tiny viewport did not receive its compact fallback prompt. Captured tail: {session.CapturedText[^Math.Min(1200, session.CapturedText.Length)..]}");
    session.Resize(new PseudoConsoleSession.Coord(120, 30));
    await Task.Delay(250);
    await session.SendKeyAsync((byte)'q');
    if (!await session.WaitForExitAsync(TimeSpan.FromSeconds(5))) throw new TimeoutException("Application did not exit after Q within five seconds.");
    if (session.ExitCode != 0) throw new InvalidOperationException($"Q exit returned code {session.ExitCode}.");
    if (!session.CapturedText.Contains("\u001b[?25h", StringComparison.Ordinal))
        throw new InvalidOperationException("Cursor-show restoration sequence was missing after Q exit.");

    using var escapeSession = new PseudoConsoleSession(executable, new PseudoConsoleSession.Coord(80, 24));
    await escapeSession.StartAsync();
    var escapeDeadline = Stopwatch.StartNew();
    while (!escapeSession.CapturedText.Contains("HOLLYWOOD", StringComparison.Ordinal) && escapeDeadline.Elapsed < TimeSpan.FromSeconds(5))
    {
        if (escapeSession.HasExited) throw new InvalidOperationException($"Second launch exited before drawing (exit {escapeSession.ExitCode}).");
        await Task.Delay(50);
    }
    if (!escapeSession.CapturedText.Contains("HOLLYWOOD", StringComparison.Ordinal)) throw new TimeoutException("Second launch did not draw a scene within five seconds.");
    await escapeSession.SendKeyAsync(0x1b);
    if (!await escapeSession.WaitForExitAsync(TimeSpan.FromSeconds(5)) || escapeSession.ExitCode != 0) throw new InvalidOperationException("Escape did not exit the second session cleanly.");

    Console.WriteLine($"PASS: ConPTY scene, animation (+{animatedBytes - firstFrameBytes} bytes), tiny/rapid resize, Q/Escape exits, cursor-show restoration; captured {session.CapturedByteCount} bytes in the Q scenario.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}

internal sealed class PseudoConsoleSession : IDisposable
{
    private const nuint ProcThreadAttributePseudoConsole = 0x00020016;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private readonly string executable;
    private readonly Coord initialSize;
    private IntPtr pseudoConsole;
    private IntPtr processHandle;
    private IntPtr threadHandle;
    private FileStream? input;
    private FileStream? output;
    private Task? drainTask;
    private readonly BoundedCapture capture = new(1_000_000);

    public PseudoConsoleSession(string executablePath, Coord size) { executable = executablePath; initialSize = size; }
    public string CapturedText => capture.Text;
    public int CapturedByteCount => capture.Length;
    public bool HasExited => processHandle != IntPtr.Zero && WaitForSingleObject(processHandle, 0) == 0;
    public int ExitCode { get { if (processHandle == IntPtr.Zero || !GetExitCodeProcess(processHandle, out var code)) throw new Win32Exception(Marshal.GetLastWin32Error()); return unchecked((int)code); } }

    public async Task StartAsync()
    {
        var inputRead = CreatePipe(out var inputWrite);
        var outputRead = CreatePipe(out var outputWrite);
        try
        {
            var result = CreatePseudoConsole(initialSize, inputRead, outputWrite, 0, out pseudoConsole);
            if (result != 0) Marshal.ThrowExceptionForHR(result);

            var attributeBytes = IntPtr.Zero;
            var attributeBuffer = IntPtr.Zero;
            try
            {
                _ = InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeBytes);
                if (attributeBytes == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not size the process attribute list.");
                attributeBuffer = Marshal.AllocHGlobal(attributeBytes);
                if (!InitializeProcThreadAttributeList(attributeBuffer, 1, 0, ref attributeBytes)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!UpdateProcThreadAttribute(attributeBuffer, 0, ProcThreadAttributePseudoConsole, pseudoConsole, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var startup = new StartupInfoEx { StartupInfo = new StartupInfo { Cb = Marshal.SizeOf<StartupInfoEx>() }, AttributeList = attributeBuffer };
                var commandLine = new StringBuilder($"\"{executable}\" --no-config");
                if (!CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, ExtendedStartupInfoPresent, IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out var processInfo))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not launch the target process under ConPTY.");
                processHandle = processInfo.Process;
                threadHandle = processInfo.Thread;
            }
            finally
            {
                if (attributeBuffer != IntPtr.Zero) { DeleteProcThreadAttributeList(attributeBuffer); Marshal.FreeHGlobal(attributeBuffer); }
            }

            input = new FileStream(new SafeFileHandle(inputWrite, ownsHandle: true), FileAccess.Write, 4096, isAsync: false);
            inputWrite = IntPtr.Zero;
            output = new FileStream(new SafeFileHandle(outputRead, ownsHandle: true), FileAccess.Read, 4096, isAsync: false);
            outputRead = IntPtr.Zero;
            drainTask = DrainOutputAsync();
        }
        finally
        {
            CloseIfValid(inputRead);
            CloseIfValid(outputWrite);
            CloseIfValid(inputWrite);
            CloseIfValid(outputRead);
        }
        await Task.Yield();
    }

    public void Resize(Coord size)
    {
        if (pseudoConsole == IntPtr.Zero) throw new InvalidOperationException("ConPTY session is not active.");
        var result = ResizePseudoConsole(pseudoConsole, size);
        if (result != 0) Marshal.ThrowExceptionForHR(result);
    }

    public async Task SendKeyAsync(byte key)
    {
        if (input is null) throw new InvalidOperationException("ConPTY input is not available.");
        await input.WriteAsync(new[] { key });
        await input.FlushAsync();
    }

    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        if (processHandle == IntPtr.Zero) return false;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (HasExited) { if (drainTask is not null) await Task.WhenAny(drainTask, Task.Delay(500)); return true; }
            await Task.Delay(25);
        }
        return HasExited;
    }

    private async Task DrainOutputAsync()
    {
        if (output is null) return;
        var buffer = new byte[8192];
        while (true)
        {
            var count = await output.ReadAsync(buffer);
            if (count == 0) return;
            capture.Add(buffer.AsSpan(0, count));
        }
    }

    public void Dispose()
    {
        input?.Dispose(); input = null;
        output?.Dispose(); output = null;
        if (processHandle != IntPtr.Zero)
        {
            if (!HasExited)
            {
                TerminateProcess(processHandle, 124);
                WaitForSingleObject(processHandle, 5000);
            }
            CloseHandle(processHandle); processHandle = IntPtr.Zero;
        }
        if (threadHandle != IntPtr.Zero) { CloseHandle(threadHandle); threadHandle = IntPtr.Zero; }
        if (pseudoConsole != IntPtr.Zero) { ClosePseudoConsole(pseudoConsole); pseudoConsole = IntPtr.Zero; }
    }

    private static IntPtr CreatePipe(out IntPtr writeHandle)
    {
        var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
        if (!CreatePipeNative(out var read, out writeHandle, ref security, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return read;
    }

    private static void CloseIfValid(IntPtr handle) { if (handle != IntPtr.Zero && handle != new IntPtr(-1)) CloseHandle(handle); }

    [StructLayout(LayoutKind.Sequential)] internal readonly record struct Coord(short X, short Y)
    {
        public Coord(int x, int y) : this(checked((short)x), checked((short)y)) { }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr SecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int Cb; public string? Reserved; public string? Desktop; public string? Title; public int X; public int Y; public int XSize; public int YSize; public int XCountChars; public int YCountChars; public int FillAttribute; public int Flags; public short ShowWindow; public short Reserved2; public IntPtr Reserved2Pointer; public IntPtr StdInput; public IntPtr StdOutput; public IntPtr StdError; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process; public IntPtr Thread; public uint ProcessId; public uint ThreadId; }

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CreatePipe")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreatePipeNative(out IntPtr readPipe, out IntPtr writePipe, ref SecurityAttributes attributes, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, IntPtr size, IntPtr previousValue, IntPtr returnSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcess(string? applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string? currentDirectory, ref StartupInfoEx startupInfo, out ProcessInformation processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output, uint flags, out IntPtr pseudoConsole);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern int ResizePseudoConsole(IntPtr pseudoConsole, Coord size);
    [DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(IntPtr pseudoConsole);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}

internal sealed class BoundedCapture
{
    private readonly int limit;
    private readonly Queue<byte> bytes = new();
    private readonly object gate = new();
    public BoundedCapture(int maximumBytes) => limit = maximumBytes;
    public int Length { get { lock (gate) return bytes.Count; } }
    public string Text { get { lock (gate) return Encoding.UTF8.GetString(bytes.ToArray()); } }
    public void Add(ReadOnlySpan<byte> data)
    {
        lock (gate)
        {
            foreach (var value in data) { if (bytes.Count == limit) bytes.Dequeue(); bytes.Enqueue(value); }
        }
    }
}
