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

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.25, 4);
        var random = context.Random;
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
        canvas.Write(bounds.X + 2, bounds.Y, "NETWORK MAP / SIM", 1, bounds);
        if (bounds.Height < 6) return;

        var splitStats = bounds.Width >= 54;
        var graphWidth = splitStats ? bounds.Width - 17 : bounds.Width - 2;
        var center = bounds.X + 1 + Math.Max(12, graphWidth / 2);
        if (bounds.Height >= 14)
        {
            canvas.Write(bounds.X + 3, bounds.Y + 1, "[ SENSOR CLUSTER ]", 6, bounds);
            canvas.Write(bounds.X + 4, bounds.Y + 2, "192.0.2.0/24", 2, bounds);
            canvas.Write(center - 1, bounds.Y + 3, activeLink == 0 ? "*" : "|", activeLink == 0 ? (byte)3 : (byte)6, bounds);
            canvas.Write(center - 8, bounds.Y + 4, "[ relay-02 ]", activeLink == 0 ? (byte)3 : (byte)7, bounds);
            canvas.Write(center - 1, bounds.Y + 5, activeLink == 1 ? "*" : "|", activeLink == 1 ? (byte)4 : (byte)6, bounds);
            canvas.Write(center - 8, bounds.Y + 6, "[ core-01 ]", activeLink is 1 or 2 ? (byte)4 : (byte)2, bounds);
            canvas.Write(center - 1, bounds.Y + 7, activeLink == 2 ? "*" : "|", activeLink == 2 ? (byte)4 : (byte)6, bounds);
            canvas.Write(center - 10, bounds.Y + 8, "[ analyzer ]", 2, bounds);
            canvas.Write(center - 1, bounds.Y + 9, activeLink == 3 ? "*" : "|", activeLink == 3 ? (byte)5 : (byte)6, bounds);
            canvas.Write(bounds.X + 3, bounds.Y + 10, "[ edge-01 ] ---- [ edge-03 ]", 2, bounds);
            canvas.Write(bounds.X + 3, bounds.Y + 11, "198.51.100.0/24 | FIXTURE LINKS", 6, bounds);
        }
        else
        {
            canvas.Write(bounds.X + 3, bounds.Y + 1, "[sensor] 192.0.2.0/24", 2, bounds);
            canvas.Write(bounds.X + 4, bounds.Y + 2, activeLink == 0 ? "*" : "|", activeLink == 0 ? (byte)3 : (byte)6, bounds);
            canvas.Write(bounds.X + 3, bounds.Y + 3, "[relay-02] -> [core-01]", 7, bounds);
            canvas.Write(bounds.X + 4, bounds.Y + 4, activeLink == 1 ? "*" : "|", activeLink == 1 ? (byte)4 : (byte)6, bounds);
            canvas.Write(bounds.X + 3, bounds.Y + 5, "[analyzer] -> [edge nodes]", 2, bounds);
            if (bounds.Height > 8)
                canvas.Write(bounds.X + 3, bounds.Bottom - 2, $"{flows / 1000.0:0.0}k flows/s  {events / 1000.0:0.0}k evt/s", 1, bounds);
        }

        if (splitStats)
        {
            var sx = bounds.Right - 16;
            canvas.Set(sx - 1, bounds.Y + 1, '|', 6);
            canvas.Set(sx - 1, bounds.Y + 2, '|', 6);
            canvas.Write(sx, bounds.Y + 1, "NODES 05", 6, bounds);
            canvas.Write(sx, bounds.Y + 2, "LINKS 04", 7, bounds);
            canvas.Write(sx, bounds.Y + 4, $"FLOW {flows / 1000.0:0.0}k", 1, bounds);
            canvas.Write(sx, bounds.Y + 5, $"EVT  {events / 1000.0:0.0}k", 2, bounds);
            if (bounds.Height > 8) canvas.Write(sx, bounds.Y + 7, "STATE OK", 1, bounds);
        }
    }

    public void Dispose() { }
}
