using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class SystemMetricsModule : IVisualModule
{
    private int cpu = 42;
    private int memory = 61;
    private int tick;
    private TimeSpan accumulated;
    public string Title => "SYSTEM  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        if (accumulated < TimeSpan.FromMilliseconds(500)) return;
        accumulated = TimeSpan.Zero;
        cpu = Math.Clamp(cpu + random.Next(17) - 8, 8, 96);
        memory = Math.Clamp(memory + random.Next(7) - 3, 24, 91);
        tick++;
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        DrawMeter(canvas, bounds, bounds.Y + 3, "CPU", cpu, 1);
        DrawMeter(canvas, bounds, bounds.Y + 6, "MEM", memory, 3);
        canvas.Write(bounds.X + 2, bounds.Y + 9, $"PROC  {126 + tick % 23:D3}   UP  04:12:{tick % 60:D2}", 1, bounds);
    }

    private static void DrawMeter(Canvas canvas, Rect bounds, int row, string label, int percent, byte style)
    {
        var width = Math.Clamp(bounds.Width - 16, 3, 20);
        var filled = width * percent / 100;
        canvas.Write(bounds.X + 2, row, $"{label} [{new string('#', filled)}{new string('.', width - filled)}] {percent,3}%", style, bounds);
    }

    public void Dispose() { }
}
