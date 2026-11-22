using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class SecurityFeedModule : IVisualModule
{
    private static readonly string[] Events =
    [
        "INFO policy check passed",
        "INFO cipher rotation complete",
        "WARN simulated alert reviewed",
        "INFO identity token renewed",
        "OK integrity baseline stable",
        "WARN sandbox event contained"
    ];
    private readonly Queue<string> entries = new();
    private TimeSpan accumulated;
    public string Title => "SECURITY ACTIVITY  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(650))
        {
            accumulated -= TimeSpan.FromMilliseconds(650);
            entries.Enqueue($"{random.Next(24):D2}:{random.Next(60):D2} {Events[random.Next(Events.Length)]}");
            while (entries.Count > 24) entries.Dequeue();
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        var row = bounds.Y + 2;
        foreach (var entry in entries.Reverse())
        {
            if (row >= bounds.Bottom - 1) break;
            canvas.Write(bounds.X + 2, row++, entry, entry.Contains("WARN", StringComparison.Ordinal) ? (byte)3 : (byte)1, bounds);
        }
    }

    public void Dispose() => entries.Clear();
}
