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

static float MeasureSteadyActivity(int keysPerSecond)
{
    var time = new ManualTimeProvider();
    var controller = new KeyboardActivityController(time);
    var interval = TimeSpan.FromSeconds(1d / keysPerSecond);
    for (var index = 0; index < keysPerSecond * 3; index++)
    {
        controller.RecordKeyEvent();
        time.Advance(interval);
        controller.Update();
    }
    return controller.Activity;
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
Check(themedCapture.ToString().Contains("38;5;220", StringComparison.Ordinal), "Retro Amber theme did not emit its palette.");
themedCapture.GetStringBuilder().Clear();
themedRenderer = new VtRenderer(themedCapture) { ThemeIndex = 4, ColorMode = "truecolor" };
themedRenderer.Write(themedCanvas);
Check(themedCapture.ToString().Contains("38;2;255;190;60", StringComparison.Ordinal), "Truecolor palette was not emitted.");
themedCapture.GetStringBuilder().Clear();
var socRenderer = new VtRenderer(themedCapture) { ThemeIndex = 2, ColorMode = "truecolor" };
var socCanvas = new Canvas(8, 1);
socCanvas.Write(0, 0, "LIME CYAN", 1);
socCanvas.Write(4, 0, "CYAN", 2);
socRenderer.Write(socCanvas);
Check(themedCapture.ToString().Contains("38;2;57;255;110", StringComparison.Ordinal)
    && themedCapture.ToString().Contains("38;2;35;235;220", StringComparison.Ordinal), "SOC palette did not emit distinct lime and cyan accents.");

var first = new DeterministicRandom(12345);
var second = new DeterministicRandom(12345);
for (var i = 0; i < 1000; i++) Check(first.NextUInt() == second.NextUInt(), "Seeded random streams diverged.");

var manualClock = new ManualTimeProvider();
var activityController = new KeyboardActivityController(manualClock, cooldownSeconds: 3d);
Check(Math.Abs(activityController.Update() - 0.05f) < 0.001f, "Keyboard activity did not begin at idle intensity.");
activityController.RecordKeyEvent();
var singleKeyActivity = activityController.Update();
Check(singleKeyActivity > 0.05f && singleKeyActivity < 0.25f, "An isolated key did not produce a small bounded activity increase.");
for (var i = 0; i < 40; i++) activityController.RecordKeyEvent();
var rapidActivity = activityController.Update();
Check(rapidActivity > 0.9f && rapidActivity <= 1f, "Rapid key events did not reach high bounded activity.");
manualClock.Advance(TimeSpan.FromSeconds(1));
var coolingActivity = activityController.Update();
Check(coolingActivity < rapidActivity && coolingActivity > 0.05f, "Activity did not decay smoothly after input stopped.");
manualClock.Advance(TimeSpan.FromSeconds(10));
Check(activityController.Update() < 0.1f, "Activity did not return near idle after cooldown.");
var slowTypingActivity = MeasureSteadyActivity(2);
var moderateTypingActivity = MeasureSteadyActivity(5);
var rapidTypingActivity = MeasureSteadyActivity(10);
Check(slowTypingActivity is >= 0.15f and < 0.4f, $"Slow typing activity was outside its expected light range: {slowTypingActivity:0.00}.");
Check(moderateTypingActivity > slowTypingActivity && moderateTypingActivity is >= 0.4f and < 0.75f, $"Moderate typing did not produce a moderate increase: {moderateTypingActivity:0.00}.");
Check(rapidTypingActivity > moderateTypingActivity && rapidTypingActivity >= 0.85f, $"Rapid typing did not approach maximum activity: {rapidTypingActivity:0.00}.");
for (var i = 0; i < 1_000; i++) activityController.RecordKeyEvent();
Check(activityController.Update() <= 1f, "A held/repeated key exceeded the activity cap.");
activityController.SetPaused(true);
for (var i = 0; i < 10; i++) activityController.RecordKeyEvent();
Check(Math.Abs(activityController.Update() - 0.05f) < 0.001f, "Paused activity controller did not stay idle.");
activityController.SetPaused(false);
activityController.SetEnabled(false);
activityController.RecordKeyEvent();
Check(Math.Abs(activityController.Update() - 0.05f) < 0.001f, "Disabled activity controller accepted key activity.");

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
        "{\"schemaVersion\":1,\"keyboardReactive\":{\"enabled\":true,\"idleIntensity\":0.9,\"maximumIntensity\":0.5}}",
        "{\"schemaVersion\":1,\"keyboardReactive\":{\"sensitivity\":99}}",
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
    new CodeStreamModule(), new SystemMetricsModule(), new AsciiWaveModule(),
    new NetworkTrafficModule(), new SecurityFeedModule(), new DataProcessingModule(),
    new RadarModule(), new NetworkTopologyModule(), new BuildLogsModule(),
    new DashboardMetricsModule(), new DigitalRainModule()
];
var flagshipLayout = LayoutSolver.Solve(120, 30, 7);
var flagship = new Canvas(120, 30);
var moduleRandom = new DeterministicRandom(9876);
foreach (var module in modules) module.Update(new VisualUpdateContext(TimeSpan.FromSeconds(3), moduleRandom, 0.6f));
for (var index = 0; index < flagshipLayout.Count; index++) modules[index].Render(flagship, flagshipLayout[index], TimeSpan.FromSeconds(3));
var flagshipText = flagship.ToPlainText();
foreach (var module in modules.Take(7)) Check(flagshipText.Contains(module.Title, StringComparison.Ordinal), $"Flagship panel missing title {module.Title}.");
var denseLayout = LayoutSolver.Solve(160, 45, 10);
var dense = new Canvas(160, 45);
for (var index = 0; index < denseLayout.Count; index++) modules[index].Render(dense, denseLayout[index], TimeSpan.FromSeconds(3));
var denseText = dense.ToPlainText();
foreach (var module in modules.Take(10)) Check(denseText.Contains(module.Title, StringComparison.Ordinal), $"Dense view missing title {module.Title}.");
foreach (var module in modules) module.Dispose();

var radarA = new RadarModule();
var radarB = new RadarModule();
var radarASeed = new DeterministicRandom(4321);
var radarBSeed = new DeterministicRandom(4321);
var radarBounds = new Rect(5, 2, 40, 12);
var radarFrameA = new Canvas(50, 16);
var radarFrameB = new Canvas(50, 16);
for (var tick = 0; tick < 8; tick++)
{
    radarA.Update(new VisualUpdateContext(TimeSpan.FromMilliseconds(100), radarASeed, 0.4f));
    radarB.Update(new VisualUpdateContext(TimeSpan.FromMilliseconds(100), radarBSeed, 0.4f));
}
radarA.Render(radarFrameA, radarBounds, TimeSpan.FromSeconds(1));
radarB.Render(radarFrameB, radarBounds, TimeSpan.FromSeconds(1));
Check(radarA.Title == "RADAR / THREAT SCOPE", "Radar title is missing.");
Check(radarFrameA.ToPlainText().Contains("RADAR / THREAT SCOPE", StringComparison.Ordinal), "Radar title did not render.");
Check(radarFrameA.CopyCells().SequenceEqual(radarFrameB.CopyCells()), "Radar seeded rendering is not deterministic.");
Check(radarFrameA.CopyCells().Any(static cell => cell.Style == 5), "Radar warning/threat color is missing.");
for (var y = 0; y < radarFrameA.Height; y++)
for (var x = 0; x < radarFrameA.Width; x++)
    if (!radarBounds.Contains(x, y)) Check(radarFrameA[x, y] == Cell.Blank, "Radar drew outside its panel bounds.");

var resizedCanvas = new Canvas(25, 12);
var resizedBounds = new Rect(2, 2, 20, 8);
radarA.Render(resizedCanvas, resizedBounds, TimeSpan.FromSeconds(1));
Check(resizedCanvas.ToPlainText().Contains("RADAR / SCOPE", StringComparison.Ordinal), "Radar did not adapt its title to a narrow panel.");
for (var y = 0; y < resizedCanvas.Height; y++)
for (var x = 0; x < resizedCanvas.Width; x++)
    if (!resizedBounds.Contains(x, y)) Check(resizedCanvas[x, y] == Cell.Blank, "Resized radar exceeded its panel clip.");

radarA.Suspend();
var pausedFrame = radarFrameA.CopyCells();
radarA.Update(new VisualUpdateContext(TimeSpan.FromSeconds(3), radarASeed, 0.9f));
radarA.Render(radarFrameA, radarBounds, TimeSpan.FromSeconds(4));
Check(radarFrameA.CopyCells().SequenceEqual(pausedFrame), "Suspended radar animation continued updating.");
radarA.Resume();
radarA.Update(new VisualUpdateContext(TimeSpan.FromSeconds(1), radarASeed, 0.9f));
radarA.Render(radarFrameA, radarBounds, TimeSpan.FromSeconds(5));
Check(!radarFrameA.CopyCells().SequenceEqual(pausedFrame), "Resumed radar sweep did not advance.");
radarA.Dispose();
radarB.Dispose();

Console.WriteLine($"PASS: clipping, borders, 96,000 layouts, all modules/radar, keyboard activity rates (2/s {slowTypingActivity:P0}, 5/s {moderateTypingActivity:P0}, 10/s {rapidTypingActivity:P0}), cooldown/pause/disable/cap, VT diffing, PRNG, configuration");

sealed class ManualTimeProvider : TimeProvider
{
    private long timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => timestamp;
    public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
}
