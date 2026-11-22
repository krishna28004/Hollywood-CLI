using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class CodeStreamModule : IVisualModule
{
    private static readonly string[] Snippets =
    {
        "async Frame Compose(Session s)",
        "var cells = canvas.Diff(old);",
        "if (!terminal.Ready) return;",
        "await scheduler.Tick(token);",
        "metrics.Commit(frame);",
        "return RenderResult.Ok(buffer);"
    };
    private readonly Queue<string> lines = new();
    private TimeSpan accumulated;
    public string Title => "LIVE CODE STREAM  /  C#";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(500))
        {
            accumulated -= TimeSpan.FromMilliseconds(500);
            lines.Enqueue(Snippets[random.Next(Snippets.Length)]);
            while (lines.Count > 24) lines.Dequeue();
        }
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
        if (row < bounds.Bottom - 1) canvas.Write(bounds.X + 2, row, "await session.Run();", 3, bounds);
    }

    public void Dispose() => lines.Clear();
}
