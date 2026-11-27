using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Hollywood.Cli.Configuration;
using Hollywood.Cli.Terminal;
using Hollywood.Engine.Layout;
using Hollywood.Engine.Modules;
using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Cli;

internal static class Program
{
    private static readonly string Version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    private sealed record Options(
        bool Help, bool ShowVersion, bool Snapshot, bool ReducedMotion,
        int Theme, int Panels, uint Seed, int FrameRate, string Speed, string ColorMode,
        string[] EnabledModules, KeyboardReactiveConfiguration KeyboardReactive);

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "config") return RunConfigCommand(args[1..]);
        if (!TryParse(args, out var options, out var parseError)) { Console.Error.WriteLine(parseError); return 2; }
        if (options.Help) { PrintHelp(); return 0; }
        if (options.ShowVersion) { Console.WriteLine($"Hollywood CLI {Version}"); return 0; }
        if (options.Snapshot) { PrintSnapshot(options); return 0; }
        if (!TerminalSessionFactory.TryAcquire(out var lease, out var error)) { Console.Error.WriteLine(error); return 3; }

        using (lease)
        using (var cancellation = new CancellationTokenSource())
        {
            var cancellationExitCode = 130;
            using var signals = RegisterTerminationSignals(cancellation, () => Interlocked.Exchange(ref cancellationExitCode, 143));
            Console.CancelKeyPress += Cancel;
            void Cancel(object? sender, ConsoleCancelEventArgs eventArgs) { eventArgs.Cancel = true; Interlocked.Exchange(ref cancellationExitCode, 130); cancellation.Cancel(); }
            try { Run(lease!, cancellation.Token, options); return cancellation.IsCancellationRequested ? Volatile.Read(ref cancellationExitCode) : 0; }
            catch (OperationCanceledException) { return Volatile.Read(ref cancellationExitCode); }
            catch (Exception exception) { Console.Error.WriteLine($"Hollywood stopped: {exception.Message}"); return 1; }
            finally { Console.CancelKeyPress -= Cancel; }
        }
    }

    private static int RunConfigCommand(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: hollywood config path|show|validate [path]"); return 2; }
        var path = args.Length > 1 ? Path.GetFullPath(args[1]) : AppConfiguration.DefaultPath;
        if (args.Length > 2) { Console.Error.WriteLine("Too many arguments for config command."); return 2; }
        try
        {
            switch (args[0])
            {
                case "path":
                    if (args.Length != 1) throw new ArgumentException("Usage: hollywood config path");
                    Console.WriteLine(AppConfiguration.DefaultPath);
                    return 0;
                case "show":
                    Console.Write(File.Exists(path) ? AppConfiguration.Serialize(AppConfiguration.Load(path)) : AppConfiguration.Serialize(new AppConfiguration()));
                    return 0;
                case "validate":
                    _ = AppConfiguration.Load(path);
                    Console.WriteLine($"Valid configuration: {path}");
                    return 0;
                default:
                    Console.Error.WriteLine("Usage: hollywood config path|show|validate [path]");
                    return 2;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            Console.Error.WriteLine($"Configuration error: {exception.Message}");
            return 2;
        }
    }

    private static IDisposable? RegisterTerminationSignals(CancellationTokenSource cancellation, Action onTerminate)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) return null;
        var registrations = new List<PosixSignalRegistration>
        {
            PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                onTerminate();
                cancellation.Cancel();
            })
        };
        return new SignalRegistrationSet(registrations);
    }

    private sealed class SignalRegistrationSet(List<PosixSignalRegistration> registrations) : IDisposable
    {
        public void Dispose()
        {
            foreach (var registration in registrations) registration.Dispose();
        }
    }

    private static void Run(ITerminalSession terminal, CancellationToken token, Options options)
    {
        var modules = CreateModules(options.EnabledModules);
        try { RunSession(terminal, token, modules, options); }
        finally { foreach (var module in modules) module.Dispose(); }
    }

    private static void RunSession(ITerminalSession terminal, CancellationToken token, IVisualModule[] modules, Options options)
    {
        var random = new DeterministicRandom(options.Seed);
        var renderer = new VtRenderer(terminal.Output) { ThemeIndex = options.Theme, ColorMode = ResolveColorMode(options.ColorMode) };
        var clock = Stopwatch.StartNew();
        var previous = clock.Elapsed;
        var requestedCount = options.Panels;
        var interval = TimeSpan.FromSeconds(1d / options.FrameRate);
        var activity = new KeyboardActivityController(TimeProvider.System, options.KeyboardReactive.Enabled,
            options.KeyboardReactive.IdleIntensity, options.KeyboardReactive.MaximumIntensity,
            options.KeyboardReactive.Sensitivity, options.KeyboardReactive.CooldownSeconds);
        var paused = false;
        var currentActivity = activity.Activity;

        while (!token.IsCancellationRequested)
        {
            var frameStart = clock.Elapsed;
            // Bound input work per frame and never retain the key identity.
            for (var inputCount = 0; inputCount < KeyboardActivityController.MaximumEventsPerUpdate && terminal.TryReadKey(out var key); inputCount++)
            {
                if (key is ConsoleKey.Q or ConsoleKey.Escape) return;
                if (key == ConsoleKey.Spacebar) { paused = !paused; activity.SetPaused(paused); continue; }
                if (key == ConsoleKey.T) { renderer.ThemeIndex = (renderer.ThemeIndex + 1) % 5; continue; }
                if (key == ConsoleKey.L) { requestedCount = requestedCount switch { 2 => 7, 7 => 10, _ => 2 }; continue; }
                activity.RecordKeyEvent();
            }

            terminal.RefreshSize();
            var now = clock.Elapsed;
            var elapsed = now - previous;
            previous = now;
            currentActivity = activity.Update();
            if (options.ReducedMotion) { currentActivity = Math.Min(currentActivity, 0.08f); elapsed = Scale(elapsed, 0.35); }
            elapsed = Scale(elapsed, options.Speed switch { "slow" => 0.6, "fast" => 1.7, _ => 1.0 });
            var activeCount = Math.Min(VisiblePanelCount(terminal.Width, terminal.Height, requestedCount), modules.Length);
            if (!paused && activeCount > 0)
                for (var index = 0; index < activeCount; index++) modules[index].Update(new VisualUpdateContext(elapsed, random, currentActivity));

            RenderFrame(terminal.Width, terminal.Height, modules, activeCount, clock.Elapsed, options, renderer, currentActivity, paused);
            var desiredInterval = paused || currentActivity < 0.1f ? TimeSpan.FromSeconds(1d / Math.Min(options.FrameRate, 6)) : interval;
            var remaining = desiredInterval - (clock.Elapsed - frameStart);
            if (remaining > TimeSpan.Zero) Thread.Sleep(remaining);
        }
    }

    private static TimeSpan Scale(TimeSpan time, double factor) => TimeSpan.FromTicks((long)(time.Ticks * factor));

    private static string ResolveColorMode(string requested)
    {
        if (requested != "auto") return requested;
        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null || Console.IsOutputRedirected) return "mono";
        if (!OperatingSystem.IsWindows())
        {
            var colorTerm = Environment.GetEnvironmentVariable("COLORTERM");
            if (colorTerm is "truecolor" or "24bit") return "truecolor";
            var term = Environment.GetEnvironmentVariable("TERM") ?? string.Empty;
            if (!term.Contains("256color", StringComparison.OrdinalIgnoreCase)) return "16color";
        }
        return "auto";
    }

    private static int VisiblePanelCount(int width, int height, int requested)
    {
        if (width < 20 || height < 7) return 0;
        if (width < 48 || height < 14) return 1;
        if (width < 80 || height < 24) return Math.Min(requested, 2);
        if (width < 120 || height < 30) return Math.Min(requested, 4);
        return requested;
    }

    private static void RenderFrame(int width, int height, IVisualModule[] modules, int activeCount, TimeSpan elapsed, Options options, VtRenderer renderer, float activity = 0.05f, bool paused = false)
    {
        if (width < 20 || height < 7)
        {
            var tiny = new Canvas(width, height);
            tiny.Write(0, 0, "RESIZE  |  CTRL+C EXIT");
            renderer.Write(tiny);
            return;
        }

        var canvas = new Canvas(width, height);
        var rects = LayoutSolver.Solve(width, height, activeCount);
        var seconds = (long)elapsed.TotalSeconds;
        var load = 0.7 + 0.25 * Math.Sin(elapsed.TotalSeconds * 0.19);
        canvas.Write(0, 0, "UBUNTU 24.04 // HOLLYWOOD SIM |", 1);
        canvas.Write(32, 0, $"node: ubuntu-lab | kernel: 6.8.0-52 | up {seconds / 86400:D1}d {seconds / 3600 % 24:D2}:{seconds / 60 % 60:D2}:{seconds % 60:D2} | sessions: 3 | load: {load:0.00} {load + 0.04:0.00} {load + 0.11:0.00} | eth0: UP | {DateTime.UtcNow:HH:mm:ss}Z", 6);
        for (var i = 0; i < rects.Count; i++) modules[i].Render(canvas, rects[i], elapsed);
        var activityBar = new string('#', (int)Math.Round(Math.Clamp(activity, 0f, 1f) * 10)).PadRight(10, '.');
        var activityText = options.KeyboardReactive.Enabled ? $"INPUT [{activityBar}] {activity:P0}" : "INPUT REACTIVE OFF";
        canvas.Write(1, height - 1, $"Q/ESC EXIT | SPACE PAUSE | T THEME | L LAYOUT | {activityText}{(paused ? " | PAUSED" : "")} | SYNTHETIC", 3);
        renderer.Write(canvas);
    }

    private static void PrintSnapshot(Options options)
    {
        const int width = 120, height = 30;
        var modules = CreateModules(options.EnabledModules);
        try
        {
            var random = new DeterministicRandom(options.Seed);
            var canvas = new Canvas(width, height);
            var rects = LayoutSolver.Solve(width, height, options.Panels);
            for (var tick = 0; tick < 18; tick++) foreach (var module in modules) module.Update(new VisualUpdateContext(TimeSpan.FromMilliseconds(500), random, 0.05f));
            for (var index = 0; index < Math.Min(rects.Count, modules.Length); index++) modules[index].Render(canvas, rects[index], TimeSpan.FromSeconds(9));
            canvas.Write(0, 0, "UBUNTU 24.04 // HOLLYWOOD SIM |", 1);
            canvas.Write(32, 0, "node: ubuntu-lab | kernel: 6.8.0-52 | up 0d 00:00:09 | sessions: 3 | load: 0.72 0.76 0.83 | eth0: UP | 14:37:20Z", 6);
            var footer = options.KeyboardReactive.Enabled
                ? "Q/ESC EXIT | SPACE PAUSE | T THEME | L LAYOUT | INPUT [#.........] 5% | SYNTHETIC"
                : "Q/ESC EXIT | SPACE PAUSE | T THEME | L LAYOUT | INPUT REACTIVE OFF | SYNTHETIC";
            canvas.Write(1, height - 1, footer, 3);
            Console.WriteLine(canvas.ToPlainText());
        }
        finally { foreach (var module in modules) module.Dispose(); }
    }

    private static bool TryParse(string[] args, out Options options, out string error)
    {
        var configPath = AppConfiguration.DefaultPath;
        var configSpecified = false;
        var loadConfig = true;
        var help = false; var showVersion = false; var snapshot = false; var reduced = (bool?)null;
        int? themeOverride = null; int? panelsOverride = null; uint? seedOverride = null; int? frameRateOverride = null; string? speedOverride = null; bool? keyboardReactiveOverride = null;
        error = string.Empty;
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h") { help = true; continue; }
            if (argument == "--version") { showVersion = true; continue; }
            if (argument == "--snapshot") { snapshot = true; continue; }
            if (argument == "--no-config") { loadConfig = false; continue; }
            if (argument is "--config" or "--theme" or "--panels" or "--layout" or "--seed" or "--speed" or "--fps" or "--keyboard-reactive")
            {
                if (++index >= args.Length) { error = $"{argument} requires a value."; options = default!; return false; }
                var value = args[index];
                switch (argument)
                {
                    case "--config":
                        if (string.IsNullOrWhiteSpace(value)) { error = "--config requires a non-empty path."; options = default!; return false; }
                        configPath = value; configSpecified = true; break;
                    case "--theme":
                        themeOverride = ParseTheme(value);
                        if (themeOverride < 0) { error = "Theme must be classic, cyberpunk, soc, matrix, or retro."; options = default!; return false; }
                        break;
                    case "--panels":
                        if (!int.TryParse(value, out var panelCount) || panelCount is not (2 or 3 or 6 or 7 or 10)) { error = "Panel count must be 2, 3, 6, 7, or 10."; options = default!; return false; }
                        panelsOverride = panelCount; break;
                    case "--layout":
                        panelsOverride = value.ToLowerInvariant() switch { "focus" => 2, "auto" => 7, "dense" => 10, _ => -1 };
                        if (panelsOverride < 0) { error = "Layout must be auto, focus, or dense."; options = default!; return false; }
                        break;
                    case "--seed":
                        if (!uint.TryParse(value, out var seed)) { error = "Seed must be an unsigned 32-bit integer."; options = default!; return false; }
                        seedOverride = seed; break;
                    case "--speed":
                        if (value is not ("slow" or "normal" or "fast")) { error = "Speed must be slow, normal, or fast."; options = default!; return false; }
                        speedOverride = value; break;
                    case "--fps":
                        if (!int.TryParse(value, out var fps) || fps is < 1 or > 60) { error = "Frame rate must be between 1 and 60."; options = default!; return false; }
                        frameRateOverride = fps; break;
                    case "--keyboard-reactive":
                        if (value is not ("on" or "off")) { error = "--keyboard-reactive must be on or off."; options = default!; return false; }
                        keyboardReactiveOverride = value == "on"; break;
                }
                continue;
            }
            if (argument == "--reduced-motion") { reduced = true; continue; }
            error = $"Unknown option: {argument}. Try --help."; options = default!; return false;
        }
        if ((help ? 1 : 0) + (showVersion ? 1 : 0) + (snapshot ? 1 : 0) > 1) { error = "Use only one of --help, --version, and --snapshot."; options = default!; return false; }
        if (!loadConfig && args.Contains("--config", StringComparer.Ordinal)) { error = "--config cannot be combined with --no-config."; options = default!; return false; }
        if (help || showVersion)
        {
            var defaults = new AppConfiguration();
            options = new Options(help, showVersion, false, false, ParseTheme(defaults.Theme), defaults.Panels, defaults.Seed, defaults.FrameRateLimit, defaults.Speed, defaults.ColorMode, defaults.EnabledModules, defaults.KeyboardReactive);
            return true;
        }
        try
        {
            var config = loadConfig && (File.Exists(configPath) || configSpecified) ? AppConfiguration.Load(configPath) : new AppConfiguration();
            if (!loadConfig && args.Contains("--config", StringComparer.Ordinal)) throw new InvalidDataException("--config cannot be combined with --no-config.");
            config = config with {
                Theme = ThemeKey(themeOverride ?? ParseTheme(config.Theme)),
                Panels = panelsOverride ?? (config.Layout switch { "focus" => 2, "dense" => 10, _ => config.Panels }),
                Seed = seedOverride ?? config.Seed,
                Speed = speedOverride ?? config.Speed,
                FrameRateLimit = frameRateOverride ?? config.FrameRateLimit,
                ReducedMotion = reduced ?? config.ReducedMotion,
                KeyboardReactive = config.KeyboardReactive with { Enabled = keyboardReactiveOverride ?? config.KeyboardReactive.Enabled }
            };
            config.Validate();
            options = new Options(help, showVersion, snapshot, config.ReducedMotion, ParseTheme(config.Theme), config.Panels, config.Seed, config.ReducedMotion ? Math.Min(config.FrameRateLimit, 8) : config.FrameRateLimit, config.Speed, config.ColorMode, config.EnabledModules, config.KeyboardReactive);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
        {
            error = $"Configuration error: {exception.Message}"; options = default!; return false;
        }
    }

    private static int ParseTheme(string theme) => theme.ToLowerInvariant() switch { "classic" => 0, "cyberpunk" => 1, "soc" => 2, "matrix" => 3, "retro" => 4, _ => -1 };
    private static string ThemeKey(int theme) => theme switch { 0 => "classic", 1 => "cyberpunk", 2 => "soc", 3 => "matrix", 4 => "retro", _ => "" };
    private static string ThemeName(int theme) => theme switch { 1 => "CYBERPUNK", 2 => "SOC", 3 => "MATRIX", 4 => "RETRO AMBER", _ => "CLASSIC" };

    private static void PrintHelp() => Console.WriteLine($"Hollywood CLI {Version}\n\nUsage: hollywood [--help|--version|--snapshot] [--config PATH|--no-config] [--theme classic|cyberpunk|soc|matrix|retro] [--panels 2|3|6|7|10] [--layout auto|focus|dense] [--speed slow|normal|fast] [--fps 1..60] [--keyboard-reactive on|off] [--reduced-motion] [--seed N]\n\nConfig: hollywood config path|show|validate [PATH]\nInteractive keys: Q/Esc quit, Ctrl+C cancel, Space pause/resume, T theme, L layout. Other keys anonymously increase simulation activity.\nAll displayed activity is synthetic; no network traffic is sent.");

    private static IVisualModule[] CreateModules(string[] enabled)
    {
        var all = new (string Id, IVisualModule Module)[]
        {
            ("code", new CodeStreamModule()), ("system", new SystemMetricsModule()), ("wave", new AsciiWaveModule()),
            ("network", new NetworkTrafficModule()), ("security", new SecurityFeedModule()), ("data", new DataProcessingModule()),
            ("radar", new RadarModule()), ("topology", new NetworkTopologyModule()),
            ("build", new BuildLogsModule()), ("dashboard", new DashboardMetricsModule()), ("rain", new DigitalRainModule())
        };
        var selected = all.Where(item => enabled.Contains(item.Id, StringComparer.Ordinal)).Select(item => item.Module).ToArray();
        foreach (var item in all.Where(item => !enabled.Contains(item.Id, StringComparer.Ordinal))) item.Module.Dispose();
        return selected;
    }
}
