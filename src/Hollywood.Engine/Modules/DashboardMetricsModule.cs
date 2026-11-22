using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class DashboardMetricsModule : IVisualModule
{
    private int events = 12_846;
    private int jobs = 342;
    private TimeSpan accumulated;
    public string Title => "OPERATIONS  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        if (accumulated < TimeSpan.FromMilliseconds(750)) return;
        accumulated = TimeSpan.Zero;
        events += random.Next(30);
        jobs = Math.Clamp(jobs + random.Next(7) - 3, 280, 410);
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 3, $"EVENTS {events:N0}", 3, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 5, $"JOBS   {jobs:N0}", 1, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 7, "STATUS NOMINAL (SIM)", 3, bounds);
    }

    public void Dispose() { }
}
