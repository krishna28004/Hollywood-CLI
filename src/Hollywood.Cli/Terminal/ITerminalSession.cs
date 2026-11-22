namespace Hollywood.Cli.Terminal;

internal interface ITerminalSession : IDisposable
{
    int Width { get; }
    int Height { get; }
    TextWriter Output { get; }
    bool RefreshSize();
    bool TryReadKey(out ConsoleKey key);
}

internal static class TerminalSessionFactory
{
    public static bool TryAcquire(out ITerminalSession? session, out string error)
    {
        if (OperatingSystem.IsWindows())
            return WindowsTerminalSession.TryAcquire(out session, out error);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return PosixTerminalSession.TryAcquire(out session, out error);

        session = null;
        error = "Interactive mode is currently supported on Windows, Linux, and macOS. Use --snapshot for plain output.";
        return false;
    }
}
