using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class NetworkTopologyModule : IVisualModule
{
    private int activeLink;
    private int flows = 12_846;
    private int events = 4_301;
    private TimeSpan accumulated;
    public string Title => "NETWORK MAP";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(240))
        {
            accumulated -= TimeSpan.FromMilliseconds(240);
            activeLink = random.Next(4);
            flows = Math.Clamp(flows + random.Next(181) - 90, 7_000, 31_000);
            events = Math.Clamp(events + random.Next(91) - 45, 1_000, 10_000);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        if (bounds.Height < 6) return;

        var center = bounds.X + bounds.Width / 2;
        canvas.Write(center - 5, bounds.Y + 1, "[relay-02]", 3, bounds);
        canvas.Write(center - 1, bounds.Y + 2, activeLink == 0 ? "*" : "|", 4, bounds);
        canvas.Write(center - 5, bounds.Y + 3, "[core-01]", activeLink is 1 or 2 ? (byte)4 : (byte)2, bounds);
        canvas.Write(center - 1, bounds.Y + 4, activeLink == 3 ? "*" : "|", 4, bounds);
        canvas.Write(center - 7, bounds.Y + 5, "[edge-01] [edge-03]", 2, bounds);
        if (bounds.Height > 7)
            canvas.Write(bounds.X + 2, bounds.Bottom - 2, $"flows {flows / 1000.0:0.0}k/s  events {events / 1000.0:0.0}k/s", 1, bounds);
    }

    public void Dispose() { }
}
