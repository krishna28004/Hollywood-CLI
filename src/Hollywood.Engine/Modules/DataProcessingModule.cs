using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class DataProcessingModule : IVisualModule
{
    private TimeSpan elapsed;
    private int progress;
    private byte[] data = new byte[16];
    public string Title => "DATA  /  SIM";

    public void Update(TimeSpan step, DeterministicRandom random)
    {
        elapsed += step;
        if (elapsed < TimeSpan.FromMilliseconds(250)) return;
        elapsed = TimeSpan.Zero;
        progress = (progress + 3) % 101;
        for (var index = 0; index < data.Length; index++) data[index] = (byte)random.Next(256);
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        var barWidth = Math.Max(4, Math.Min(bounds.Width - 10, 18));
        var filled = barWidth * progress / 100;
        canvas.Write(bounds.X + 2, bounds.Y + 2, $"BATCH-042  [{new string('#', filled)}{new string('.', barWidth - filled)}] {progress:D3}%", 1, bounds);
        var byteCount = Math.Clamp((bounds.Width - 16) / 3, 1, data.Length);
        var hex = string.Join(' ', data.Take(byteCount).Select(value => value.ToString("X2")));
        canvas.Write(bounds.X + 2, bounds.Y + 4, $"00000080  {hex}", 3, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 6, "records parsed: 001,284", 1, bounds);
    }

    public void Dispose() => data = Array.Empty<byte>();
}
