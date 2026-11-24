using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class DigitalRainModule : IVisualModule
{
    private readonly List<(int X, int Y, int Length)> streams = new();
    private TimeSpan accumulated;
    public string Title => "DIGITAL RAIN";

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.25, 3.5);
        var random = context.Random;
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(100))
        {
            accumulated -= TimeSpan.FromMilliseconds(100);
            for (var i = 0; i < streams.Count; i++)
                streams[i] = (streams[i].X, streams[i].Y + 1, streams[i].Length);
            if (random.Next(3) == 0) streams.Add((random.Next(100), 0, 4 + random.Next(10)));
            streams.RemoveAll(stream => stream.Y - stream.Length > 100);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        var interior = new Rect(bounds.X + 1, bounds.Y + 1, Math.Max(0, bounds.Width - 2), Math.Max(0, bounds.Height - 2));
        foreach (var stream in streams)
        {
            var x = bounds.X + 1 + stream.X % Math.Max(1, interior.Width);
            for (var tail = 0; tail < stream.Length; tail++)
            {
                var y = bounds.Y + 1 + stream.Y - tail;
                if (interior.Contains(x, y)) canvas.Set(x, y, "0123456789ABCDEF"[(stream.X * 7 + stream.Y + tail * 11) & 15], tail == 0 ? (byte)3 : (byte)1);
            }
        }
    }

    public void Dispose() => streams.Clear();
}
