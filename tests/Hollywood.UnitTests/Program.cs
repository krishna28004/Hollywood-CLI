using Hollywood.Engine.Layout;
using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;
using Hollywood.Engine.Modules;
using Hollywood.Cli.Terminal;
using System.Text;
using Hollywood.Cli.Configuration;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var canvas = new Canvas(20, 8);
canvas.Write(-2, 0, "ABCD");
Check(canvas[0, 0].Character == 'C' && canvas[1, 0].Character == 'D', "Clipped text did not respect canvas bounds.");
canvas.Set(5, 5, '\u001b');
Check(canvas[5, 5].Character == ' ', "Control characters must not enter the cell buffer.");
canvas.Border(new Rect(1, 1, 8, 5));
Check(canvas[1, 1].Character == '+' && canvas[8, 5].Character == '+', "Border corners were not drawn.");

foreach (var count in new[] { 1, 2, 3, 6, 7, 10 })
for (var width = 1; width <= 240; width++)
for (var height = 1; height <= 80; height++)
{
    var panels = LayoutSolver.Solve(width, height, count);
    foreach (var rect in panels)
        Check(rect.X >= 0 && rect.Y >= 0 && rect.Right <= Math.Clamp(width, 1, 240) && rect.Bottom <= Math.Clamp(height, 1, 80), "Layout escaped viewport.");
    for (var i = 0; i < panels.Count; i++)
    for (var j = i + 1; j < panels.Count; j++)
    {
        var a = panels[i]; var b = panels[j];
        var overlaps = a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
        Check(!overlaps, "Layout panels overlap.");
    }
}

var capture = new StringWriter();
var vtRenderer = new VtRenderer(capture);
var smallFrame = new Canvas(20, 8);
smallFrame.Write(0, 0, "FIRST FRAME");
vtRenderer.Write(smallFrame);
Check(capture.ToString().Contains("FIRST FRAME", StringComparison.Ordinal), "Initial VT frame omitted visible content.");
Check(!capture.ToString().Contains("\u001b[8;20H", StringComparison.Ordinal), "Renderer wrote the lower-right terminal cell.");
capture.GetStringBuilder().Clear();
vtRenderer.Write(smallFrame);
Check(capture.ToString().Length == 0, "Unchanged frame emitted terminal output.");
vtRenderer.Write(new Canvas(22, 8));
Check(capture.ToString().Contains("\u001b[2J", StringComparison.Ordinal), "Resize did not clear stale cells outside the new viewport.");
capture.GetStringBuilder().Clear();
smallFrame.Set(3, 3, 'X');
vtRenderer.Write(smallFrame);
Check(capture.ToString().Contains("X", StringComparison.Ordinal), "Dirty cell was not emitted.");
capture.GetStringBuilder().Clear();
vtRenderer.Invalidate();
vtRenderer.Write(smallFrame);
Check(capture.ToString().Length > 0, "Invalidating the renderer did not request a full repaint.");
var themedCapture = new StringWriter();
var themedRenderer = new VtRenderer(themedCapture) { ThemeIndex = 4 };
var themedCanvas = new Canvas(6, 2);
themedCanvas.Write(0, 0, "AMBER", 2);
themedRenderer.Write(themedCanvas);
Check(themedCapture.ToString().Contains("38;5;214", StringComparison.Ordinal), "Retro Amber theme did not emit its palette.");
themedCapture.GetStringBuilder().Clear();
themedRenderer = new VtRenderer(themedCapture) { ThemeIndex = 4, ColorMode = "truecolor" };
themedRenderer.Write(themedCanvas);
Check(themedCapture.ToString().Contains("38;2;255;170;35", StringComparison.Ordinal), "Truecolor palette was not emitted.");

var first = new DeterministicRandom(12345);
var second = new DeterministicRandom(12345);
for (var i = 0; i < 1000; i++) Check(first.NextUInt() == second.NextUInt(), "Seeded random streams diverged.");

var configPath = Path.Combine(Path.GetTempPath(), $"hollywood-config-test-{Guid.NewGuid():N}.json");
if (OperatingSystem.IsWindows())
    Check(AppConfiguration.DefaultPath.EndsWith(Path.Combine("Hollywood", "config.json"), StringComparison.Ordinal), "Windows config path must use LocalAppData/Hollywood.");
else if (OperatingSystem.IsMacOS())
    Check(AppConfiguration.DefaultPath.EndsWith(Path.Combine("Library", "Application Support", "Hollywood", "config.json"), StringComparison.Ordinal), "macOS config path must use Application Support.");
else if (OperatingSystem.IsLinux())
{
    var originalXdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
    var testXdg = Path.Combine(Path.GetTempPath(), $"hollywood-xdg-{Guid.NewGuid():N}");
    try
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", testXdg);
        Check(AppConfiguration.DefaultPath == Path.Combine(testXdg, "hollywood", "config.json"), "Linux config path must honor absolute XDG_CONFIG_HOME.");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", "relative-config");
        Check(AppConfiguration.DefaultPath.EndsWith(Path.Combine(".config", "hollywood", "config.json"), StringComparison.Ordinal), "Linux config path must ignore relative XDG_CONFIG_HOME.");
    }
    finally { Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", originalXdg); }
}
try
{
    File.WriteAllText(configPath, AppConfiguration.Serialize(new AppConfiguration { Theme = "retro", Panels = 10, FrameRateLimit = 30 }));
    var loadedConfig = AppConfiguration.Load(configPath);
    Check(loadedConfig.Theme == "retro" && loadedConfig.Panels == 10 && loadedConfig.FrameRateLimit == 30, "Valid configuration did not round-trip.");
    foreach (var invalidJson in new[]
    {
        "{\"schemaVersion\":2}",
        "{\"schemaVersion\":1,\"theme\":\"unknown\"}",
        "{\"schemaVersion\":1,\"unexpected\":true}",
        "{\"schemaVersion\":1,\"panels\":6,\"panels\":10}"
    })
    {
        File.WriteAllText(configPath, invalidJson);
        var rejected = false;
        try { _ = AppConfiguration.Load(configPath); }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
        Check(rejected, $"Invalid config was accepted: {invalidJson}");
    }
}
finally { if (File.Exists(configPath)) File.Delete(configPath); }

IVisualModule[] modules =
[
    new CodeStreamModule(), new NetworkTrafficModule(), new DigitalRainModule(),
    new DataProcessingModule(), new SecurityFeedModule(), new SystemMetricsModule(),
    new BuildLogsModule(), new AsciiWaveModule(), new NetworkTopologyModule(), new DashboardMetricsModule()
];
var flagshipLayout = LayoutSolver.Solve(120, 30, 7);
var flagship = new Canvas(120, 30);
var moduleRandom = new DeterministicRandom(9876);
foreach (var module in modules) module.Update(TimeSpan.FromSeconds(3), moduleRandom);
for (var index = 0; index < flagshipLayout.Count; index++) modules[index].Render(flagship, flagshipLayout[index], TimeSpan.FromSeconds(3));
var flagshipText = flagship.ToPlainText();
foreach (var module in modules.Take(7)) Check(flagshipText.Contains(module.Title, StringComparison.Ordinal), $"Flagship panel missing title {module.Title}.");
var denseLayout = LayoutSolver.Solve(160, 45, 10);
var dense = new Canvas(160, 45);
for (var index = 0; index < denseLayout.Count; index++) modules[index].Render(dense, denseLayout[index], TimeSpan.FromSeconds(3));
var denseText = dense.ToPlainText();
foreach (var module in modules) Check(denseText.Contains(module.Title, StringComparison.Ordinal), $"Dense view missing title {module.Title}.");
foreach (var module in modules) module.Dispose();

Console.WriteLine("PASS: clipping, borders, 96,000 layout cases, flagship seven-panel and dense ten-panel layouts, ten modules, VT diffing, bottom-right protection, deterministic PRNG, five-theme configuration validation");
