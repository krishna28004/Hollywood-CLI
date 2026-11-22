using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class NetworkTopologyModule : IVisualModule
{
    private int activeLink;
    private TimeSpan accumulated;
    public string Title => "TOPOLOGY  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        if (accumulated < TimeSpan.FromMilliseconds(600)) return;
        accumulated = TimeSpan.Zero;
        activeLink = random.Next(3);
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        var center = bounds.X + bounds.Width / 2 - 4;
        canvas.Write(center, bounds.Y + 2, "[ CORE ]", 3, bounds);
        canvas.Write(center + 3, bounds.Y + 3, "|", 1, bounds);
        canvas.Write(center + 3, bounds.Y + 4, activeLink == 0 ? "*" : "|", 3, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 5, "[EDGE-A] [EDGE-B] [EDGE-C]", 1, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 7, "LINKS 3/3   HEALTHY", 3, bounds);
    }

    public void Dispose() { }
}
