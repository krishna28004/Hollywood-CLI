using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class AsciiWaveModule : IVisualModule
{
    private double phase;
    public string Title => "SIGNAL WAVE  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random) => phase += elapsed.TotalSeconds * 2.4;

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        var middle = bounds.Y + bounds.Height / 2;
        var width = Math.Max(0, bounds.Width - 4);
        for (var x = 0; x < width; x++)
        {
            var y = middle + (int)Math.Round(Math.Sin(phase + x * 0.34) * Math.Max(1, bounds.Height / 4));
            canvas.Set(bounds.X + 2 + x, y, '*', 3);
        }
        canvas.Write(bounds.X + 2, bounds.Bottom - 2, $"FREQ {1.2 + Math.Sin(phase) * 0.2:0.0} KHZ", 1, bounds);
    }

    public void Dispose() { }
}
