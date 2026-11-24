using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Hollywood.Cli.Terminal;

/// <summary>
/// POSIX adapter. Linux and Darwin use distinct termios layouts, so input mode changes
/// are isolated here and the complete original structure is restored during disposal.
/// </summary>
internal sealed class PosixTerminalSession : ITerminalSession
{
    private const string EnterAlternateScreen = "\u001b[?1049h";
    private const string HideCursor = "\u001b[?25l";
    private const string RestoreTerminal = "\u001b[0m\u001b[?25h\u001b[?1049l";
    private const int TerminalAttributesBufferSize = 128;
    private const int Tcsanow = 0;
    private bool terminalModesEntered;
    private IntPtr originalTerminalAttributes;
    private bool disposed;
    private int escapeState;
    private long escapeStarted;
    private int sequenceLength;
    private int utf8ContinuationBytes;

    private PosixTerminalSession() { }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public TextWriter Output => Console.Out;

    public static bool TryAcquire(out ITerminalSession? session, out string error)
    {
        session = null;
        error = string.Empty;
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            error = "Interactive mode requires an attached terminal for both input and output. Use --snapshot for redirected output.";
            return false;
        }

        var term = Environment.GetEnvironmentVariable("TERM");
        if (string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
        {
            error = "The current terminal reports TERM=dumb and cannot provide the interactive display. Use --snapshot for plain output.";
            return false;
        }

        var acquired = new PosixTerminalSession();
        try
        {
            if (!acquired.RefreshSize())
            {
                error = "Could not determine terminal dimensions. Use --snapshot for plain output.";
                return false;
            }

            acquired.EnterCharacterInputMode();

            // Common xterm-compatible DEC private modes used by Terminal.app, iTerm2,
            // GNOME Terminal, Konsole, xterm, and tmux/screen passthrough environments.
            acquired.terminalModesEntered = true;
            acquired.Output.Write(EnterAlternateScreen);
            acquired.Output.Write(HideCursor);
            acquired.Output.Write("\u001b[0m");
            acquired.Output.Flush();
            session = acquired;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            acquired.Dispose();
            error = $"Could not initialize the POSIX terminal: {exception.Message}";
            return false;
        }
    }

    public bool RefreshSize()
    {
        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            if (width <= 0 || height <= 0) return false;
            Width = Math.Clamp(width, 1, 240);
            Height = Math.Clamp(height, 1, 80);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public bool TryReadKey(out ConsoleKey key)
    {
        key = default;
        while (ReadByte(0, out var input) == 1)
        {
            if (utf8ContinuationBytes > 0)
            {
                if ((input & 0xC0) == 0x80) { utf8ContinuationBytes--; continue; }
                utf8ContinuationBytes = 0;
            }
            if (escapeState == 1)
            {
                if (input == (byte)'[' || input == (byte)'O') { escapeState = input == (byte)'[' ? 2 : 3; sequenceLength = 0; continue; }
                escapeState = 0;
                if (TryTranslateByte(input, out key)) return true;
                continue;
            }
            if (escapeState is 2 or 3)
            {
                sequenceLength++;
                if (sequenceLength > 16) { escapeState = 0; continue; }
                if (input is >= 0x40 and <= 0x7e)
                {
                    key = TranslateSequence(input, escapeState);
                    escapeState = 0;
                    if (key != ConsoleKey.NoName) return true;
                }
                continue;
            }
            if (input == 0x1b) { escapeState = 1; escapeStarted = Stopwatch.GetTimestamp(); continue; }
            // Count one UTF-8 code point, not each continuation byte.
            if (input is >= 0xC2 and <= 0xDF) { utf8ContinuationBytes = 1; key = ConsoleKey.NoName; return true; }
            if (input is >= 0xE0 and <= 0xEF) { utf8ContinuationBytes = 2; key = ConsoleKey.NoName; return true; }
            if (input is >= 0xF0 and <= 0xF4) { utf8ContinuationBytes = 3; key = ConsoleKey.NoName; return true; }
            if ((input & 0xC0) == 0x80) continue;
            if (TryTranslateByte(input, out key)) return true;
        }
        if (escapeState == 1 && Stopwatch.GetElapsedTime(escapeStarted) >= TimeSpan.FromMilliseconds(40))
        {
            escapeState = 0;
            key = ConsoleKey.Escape;
            return true;
        }
        return false;
    }

    private static bool TryTranslateByte(byte input, out ConsoleKey key)
    {
        key = input switch
        {
            (byte)'q' or (byte)'Q' => ConsoleKey.Q,
            (byte)'t' or (byte)'T' => ConsoleKey.T,
            (byte)'l' or (byte)'L' => ConsoleKey.L,
            (byte)' ' => ConsoleKey.Spacebar,
            0x08 or 0x7f => ConsoleKey.Backspace,
            0x09 => ConsoleKey.Tab,
            0x0d or 0x0a => ConsoleKey.Enter,
            (byte)'0' => ConsoleKey.D0, (byte)'1' => ConsoleKey.D1, (byte)'2' => ConsoleKey.D2,
            (byte)'3' => ConsoleKey.D3, (byte)'4' => ConsoleKey.D4, (byte)'5' => ConsoleKey.D5,
            (byte)'6' => ConsoleKey.D6, (byte)'7' => ConsoleKey.D7, (byte)'8' => ConsoleKey.D8, (byte)'9' => ConsoleKey.D9,
            >= (byte)'A' and <= (byte)'Z' => (ConsoleKey)((int)ConsoleKey.A + input - (byte)'A'),
            >= (byte)'a' and <= (byte)'z' => (ConsoleKey)((int)ConsoleKey.A + input - (byte)'a'),
            >= 0x20 and <= 0x7e => ConsoleKey.NoName,
            _ => ConsoleKey.NoName
        };
        return input is >= 0x20 and <= 0x7e or 0x08 or 0x7f or 0x09 or 0x0d or 0x0a;
    }

    private static ConsoleKey TranslateSequence(byte final, int state) => final switch
    {
        (byte)'A' => ConsoleKey.UpArrow, (byte)'B' => ConsoleKey.DownArrow,
        (byte)'C' => ConsoleKey.RightArrow, (byte)'D' => ConsoleKey.LeftArrow,
        (byte)'H' => ConsoleKey.Home, (byte)'F' => ConsoleKey.End,
        (byte)'~' => ConsoleKey.Delete,
        _ when state == 3 && final is (byte)'P' or (byte)'Q' or (byte)'R' or (byte)'S' => (ConsoleKey)((int)ConsoleKey.F1 + final - (byte)'P'),
        _ => ConsoleKey.NoName
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RestoreInputMode();
        if (!terminalModesEntered) return;
        try
        {
            Output.Write(RestoreTerminal);
            Output.Flush();
        }
        catch (IOException) { /* Best-effort cleanup; the OS restores normal terminal behavior on process exit. */ }
        catch (InvalidOperationException) { /* Best-effort cleanup if the host closed its output stream. */ }
    }

    private void EnterCharacterInputMode()
    {
        originalTerminalAttributes = Marshal.AllocHGlobal(TerminalAttributesBufferSize);
        var workingAttributes = Marshal.AllocHGlobal(TerminalAttributesBufferSize);
        try
        {
            Marshal.Copy(new byte[TerminalAttributesBufferSize], 0, originalTerminalAttributes, TerminalAttributesBufferSize);
            if (GetTerminalAttributes(0, originalTerminalAttributes) != 0)
                throw new IOException("Could not read the current terminal input settings.");

            var attributes = new byte[TerminalAttributesBufferSize];
            Marshal.Copy(originalTerminalAttributes, attributes, 0, attributes.Length);
            if (OperatingSystem.IsMacOS())
            {
                // Darwin tcflag_t is unsigned long, NCCS is 20, VMIN=16, VTIME=17.
                var localFlags = BitConverter.ToUInt64(attributes, 24);
                localFlags &= ~(0x00000100UL | 0x00000008UL); // ICANON | ECHO; keep ISIG.
                BitConverter.GetBytes(localFlags).CopyTo(attributes, 24);
                attributes[32 + 16] = 0; // VMIN: make each nonblocking read return immediately.
                attributes[32 + 17] = 0; // VTIME
            }
            else
            {
                // glibc Linux tcflag_t is uint; c_line precedes the 32-byte c_cc array.
                var localFlags = BitConverter.ToUInt32(attributes, 12);
                localFlags &= ~(0x00000002U | 0x00000008U); // ICANON | ECHO; keep ISIG.
                BitConverter.GetBytes(localFlags).CopyTo(attributes, 12);
                attributes[17 + 6] = 0; // VMIN: make each nonblocking read return immediately.
                attributes[17 + 5] = 0; // VTIME
            }
            Marshal.Copy(attributes, 0, workingAttributes, attributes.Length);

            if (SetTerminalAttributes(0, Tcsanow, workingAttributes) != 0)
                throw new IOException("Could not enable immediate terminal key input.");
        }
        catch
        {
            Marshal.FreeHGlobal(originalTerminalAttributes);
            originalTerminalAttributes = IntPtr.Zero;
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(workingAttributes);
        }
    }

    private void RestoreInputMode()
    {
        if (originalTerminalAttributes == IntPtr.Zero) return;
        try { _ = SetTerminalAttributes(0, Tcsanow, originalTerminalAttributes); }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Best effort: the OS restores terminal settings when the process exits.
        }
        finally
        {
            Marshal.FreeHGlobal(originalTerminalAttributes);
            originalTerminalAttributes = IntPtr.Zero;
        }
    }

    private static int GetTerminalAttributes(int descriptor, IntPtr attributes) =>
        OperatingSystem.IsMacOS()
            ? DarwinNative.GetTerminalAttributes(descriptor, attributes)
            : LinuxNative.GetTerminalAttributes(descriptor, attributes);

    private static int SetTerminalAttributes(int descriptor, int action, IntPtr attributes) =>
        OperatingSystem.IsMacOS()
            ? DarwinNative.SetTerminalAttributes(descriptor, action, attributes)
            : LinuxNative.SetTerminalAttributes(descriptor, action, attributes);

    private static nint ReadByte(int descriptor, out byte value) =>
        OperatingSystem.IsMacOS()
            ? DarwinNative.ReadByte(descriptor, out value)
            : LinuxNative.ReadByte(descriptor, out value);

    private static class LinuxNative
    {
        [DllImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
        internal static extern int GetTerminalAttributes(int descriptor, IntPtr attributes);

        [DllImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
        internal static extern int SetTerminalAttributes(int descriptor, int action, IntPtr attributes);

        [DllImport("libc", EntryPoint = "read", SetLastError = true)]
        internal static extern nint ReadByte(int descriptor, out byte value, nuint count = 1);
    }

    private static class DarwinNative
    {
        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "tcgetattr", SetLastError = true)]
        internal static extern int GetTerminalAttributes(int descriptor, IntPtr attributes);

        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "tcsetattr", SetLastError = true)]
        internal static extern int SetTerminalAttributes(int descriptor, int action, IntPtr attributes);

        [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "read", SetLastError = true)]
        internal static extern nint ReadByte(int descriptor, out byte value, nuint count = 1);
    }
}
