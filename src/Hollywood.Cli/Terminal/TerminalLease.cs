using System.Runtime.InteropServices;

namespace Hollywood.Cli.Terminal;

public sealed class TerminalLease : IDisposable
{
    private const uint EnableProcessedOutput = 0x0001;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint EnableProcessedInput = 0x0001;
    private const uint EnableWindowInput = 0x0008;
    private const uint EnableExtendedFlags = 0x0080;
    private const uint EnableQuickEditMode = 0x0040;
    private readonly IntPtr inputHandle;
    private readonly IntPtr outputHandle;
    private readonly bool ownsInputHandle;
    private readonly bool ownsOutputHandle;
    private readonly IntPtr originalStandardOutput;
    private bool standardOutputRebound;
    private readonly uint originalInputMode;
    private readonly uint originalOutputMode;
    private bool inputModeChanged;
    private bool outputModeChanged;
    private bool alternateScreenEntered;
    private bool cursorHidden;
    private bool disposed;

    public int Width { get; private set; }
    public int Height { get; private set; }

    private TerminalLease(IntPtr input, IntPtr output, uint inputMode, uint outputMode, bool ownsInput, bool ownsOutput, IntPtr originalStdOut)
    {
        inputHandle = input;
        outputHandle = output;
        originalInputMode = inputMode;
        originalOutputMode = outputMode;
        ownsInputHandle = ownsInput;
        ownsOutputHandle = ownsOutput;
        originalStandardOutput = originalStdOut;
    }

    public static bool TryAcquire(out TerminalLease? lease, out string error)
    {
        lease = null;
        error = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            error = "Interactive console handles are unavailable. Use --snapshot for plain output.";
            return false;
        }

        var input = GetStdHandle(-10);
        var output = GetStdHandle(-11);
        var ownsOutput = false;
        uint outputMode = 0;
        var hasOutputMode = output != IntPtr.Zero && output != new IntPtr(-1) && GetConsoleMode(output, out outputMode);
        if (!hasOutputMode)
        {
            output = OpenConsoleOutput();
            ownsOutput = output != IntPtr.Zero && output != new IntPtr(-1);
            if (!ownsOutput || !GetConsoleMode(output, out outputMode))
            {
                if (ownsOutput) CloseHandle(output);
                error = $"Could not open a Windows console output handle ({Marshal.GetLastWin32Error()}).";
                return false;
            }
        }

        var ownsInput = false;
        if (input == IntPtr.Zero || input == new IntPtr(-1) || !GetConsoleMode(input, out var inputMode))
        {
            input = OpenConsoleInput();
            ownsInput = input != IntPtr.Zero && input != new IntPtr(-1);
            if (!ownsInput || !GetConsoleMode(input, out inputMode))
            {
                if (ownsInput) CloseHandle(input);
                error = $"Could not open a Windows console input handle ({Marshal.GetLastWin32Error()}).";
                return false;
            }
        }

        var acquired = new TerminalLease(input, output, inputMode, outputMode, ownsInput, ownsOutput, GetStdHandle(-11));
        try
        {
            if (ownsOutput)
            {
                if (!SetStdHandle(-11, output)) throw new InvalidOperationException("Could not bind the terminal output handle.");
                acquired.standardOutputRebound = true;
            }
            if (!SetConsoleMode(output, outputMode | EnableProcessedOutput | EnableVirtualTerminalProcessing))
                throw new InvalidOperationException("VT output mode is unavailable.");
            acquired.outputModeChanged = true;

            var safeInputMode = (inputMode | EnableProcessedInput | EnableWindowInput | EnableExtendedFlags) & ~EnableQuickEditMode;
            if (safeInputMode != inputMode)
            {
                if (!SetConsoleMode(input, safeInputMode)) throw new InvalidOperationException("Could not configure console input.");
                acquired.inputModeChanged = true;
            }

            acquired.alternateScreenEntered = true;
            Console.Out.Write("\u001b[?1049h");
            acquired.cursorHidden = true;
            Console.Out.Write("\u001b[?25l\u001b[0m");
            Console.Out.Flush();
            acquired.RefreshSize();
            lease = acquired;
            return true;
        }
        catch (Exception exception)
        {
            acquired.Dispose();
            error = $"Could not initialize terminal rendering: {exception.Message}";
            return false;
        }
    }

    public bool RefreshSize()
    {
        if (GetConsoleScreenBufferInfo(outputHandle, out var info))
        {
            Width = Math.Clamp(info.Window.Right - info.Window.Left + 1, 1, 240);
            Height = Math.Clamp(info.Window.Bottom - info.Window.Top + 1, 1, 80);
            return Width > 0 && Height > 0;
        }
        try
        {
            Width = Math.Clamp(Console.WindowWidth, 1, 240);
            Height = Math.Clamp(Console.WindowHeight, 1, 80);
            return Width > 0 && Height > 0;
        }
        catch (IOException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public bool TryReadKey(out ConsoleKey key)
    {
        key = default;
        if (!GetNumberOfConsoleInputEvents(inputHandle, out var available) || available == 0) return false;
        while (available-- > 0)
        {
            if (!ReadConsoleInput(inputHandle, out var record, 1, out var read) || read != 1) return false;
            if (record.EventType != KeyEvent || !record.KeyEvent.KeyDown) continue;
            key = (ConsoleKey)record.KeyEvent.VirtualKeyCode;
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (cursorHidden) Console.Out.Write("\u001b[0m\u001b[?25h");
            if (alternateScreenEntered) Console.Out.Write("\u001b[?1049l");
            Console.Out.Flush();
        }
        catch { /* Best-effort terminal restoration. */ }
        finally
        {
            if (inputModeChanged) SetConsoleMode(inputHandle, originalInputMode);
            if (outputModeChanged) SetConsoleMode(outputHandle, originalOutputMode);
            if (standardOutputRebound) SetStdHandle(-11, originalStandardOutput);
            if (ownsInputHandle) CloseHandle(inputHandle);
            if (ownsOutputHandle) CloseHandle(outputHandle);
        }
    }

    private const ushort KeyEvent = 0x0001;
    private static IntPtr OpenConsoleInput() => CreateFile("CONIN$", 0x80000000 | 0x40000000, 0x00000001 | 0x00000002, IntPtr.Zero, 3, 0, IntPtr.Zero);
    private static IntPtr OpenConsoleOutput() => CreateFile("CONOUT$", 0x80000000 | 0x40000000, 0x00000001 | 0x00000002, IntPtr.Zero, 3, 0, IntPtr.Zero);

    [StructLayout(LayoutKind.Sequential)] private struct Coord { public short X; public short Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyEventRecord
    {
        [MarshalAs(UnmanagedType.Bool)] public bool KeyDown;
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public char UnicodeChar;
        public uint ControlKeyState;
    }
    [StructLayout(LayoutKind.Explicit)] private struct InputRecord
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KeyEventRecord KeyEvent;
    }
    [StructLayout(LayoutKind.Sequential)] private struct SmallRect { public short Left; public short Top; public short Right; public short Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ConsoleScreenBufferInfo
    {
        public Coord BufferSize;
        public Coord CursorPosition;
        public ushort Attributes;
        public SmallRect Window;
        public Coord MaximumWindowSize;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(int standardHandle);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetStdHandle(int standardHandle, IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetNumberOfConsoleInputEvents(IntPtr handle, out uint numberOfEvents);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetConsoleScreenBufferInfo(IntPtr handle, out ConsoleScreenBufferInfo info);
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadConsoleInputW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ReadConsoleInput(IntPtr handle, out InputRecord buffer, uint length, out uint numberOfEventsRead);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
