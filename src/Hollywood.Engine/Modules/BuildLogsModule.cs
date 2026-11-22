using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class BuildLogsModule : IVisualModule
{
    private static readonly string[] Stages = ["build queued", "compile objects", "link modules", "tests passed", "package staged", "build complete"];
    private readonly Queue<string> lines = new();
    private TimeSpan accumulated;
    private int stage;
    public string Title => "BUILD PIPELINE  [CI FIXTURE]";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        if (accumulated < TimeSpan.FromMilliseconds(900)) return;
        accumulated = TimeSpan.Zero;
        var clock = new DateTime(2025, 2, 14, 14, 37, 0, DateTimeKind.Utc).AddSeconds(random.Next(86400));
        lines.Enqueue($"{clock:HH:mm:ss} UTC  {Stages[stage]}");
        stage = (stage + 1) % Stages.Length;
        while (lines.Count > 20) lines.Dequeue();
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        var row = bounds.Y + 2;
        foreach (var line in lines.Reverse())
        {
            if (row >= bounds.Bottom - 1) break;
            canvas.Write(bounds.X + 2, row++, line, 1, bounds);
        }
    }

    public void Dispose() => lines.Clear();
}
