namespace Hollywood.Cli.Terminal;

/// <summary>
/// POSIX adapter that delegates terminal input-mode transitions to .NET's Console APIs,
/// avoiding direct P/Invoke against incompatible Linux/Darwin termios layouts.
/// </summary>
internal sealed class PosixTerminalSession : ITerminalSession
{
    private const string EnterAlternateScreen = "\u001b[?1049h";
    private const string HideCursor = "\u001b[?25l";
    private const string RestoreTerminal = "\u001b[0m\u001b[?25h\u001b[?1049l";
    private bool terminalModesEntered;
    private bool disposed;

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
        catch (Exception exception) when (exception is IOException or InvalidOperationException or PlatformNotSupportedException)
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
        if (!Console.KeyAvailable) return false;
        key = Console.ReadKey(intercept: true).Key;
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!terminalModesEntered) return;
        try
        {
            Output.Write(RestoreTerminal);
            Output.Flush();
        }
        catch (IOException) { /* Best-effort cleanup; the OS restores normal terminal behavior on process exit. */ }
        catch (InvalidOperationException) { /* Best-effort cleanup if the host closed its output stream. */ }
    }
}
