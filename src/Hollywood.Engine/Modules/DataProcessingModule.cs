using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class DataProcessingModule : IVisualModule
{
    private readonly Queue<(string Text, byte Style)> jsonLines = new();
    private readonly byte[] buffer = new byte[16];
    private TimeSpan accumulated;
    private int recordId = 1284;
    private int progress = 64;
    public string Title => "JSON / HEX DATA";

    public DataProcessingModule()
    {
        Enqueue("{", 0);
        Enqueue("  \"event\": \"flow\",", 2);
        Enqueue("  \"src\": \"192.0.2.17\",", 7);
        Enqueue("  \"dst\": \"198.51.100.23\",", 7);
        Enqueue("  \"proto\": \"tcp\",", 2);
        Enqueue("  \"dport\": 443,", 0);
        Enqueue("  \"bytes\": 1460,", 0);
        Enqueue("  \"risk\": \"medium\",", 5);
        Enqueue("  \"tags\": [\"tls\", \"egress\"]", 4);
        Enqueue("}", 0);
    }

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.2, 4.5);
        var random = context.Random;
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(185))
        {
            accumulated -= TimeSpan.FromMilliseconds(185);
            progress = (progress + 3 + random.Next(7)) % 100;
            recordId += random.Next(4) + 1;
            for (var index = 0; index < buffer.Length; index++) buffer[index] = (byte)random.Next(256);
            Enqueue($"  \"seq\": {recordId},", 0);
            Enqueue($"  \"rate\": {23 + random.Next(110):D2}k/s,", 3);
            Enqueue($"  \"batch\": \"{random.Next(16):X2}\"", 4);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        var visibleRows = Math.Max(0, bounds.Height - 3);
        var row = bounds.Y + 1;
        foreach (var line in jsonLines.TakeLast(visibleRows))
        {
            canvas.Write(bounds.X + 2, row++, line.Text, line.Style, bounds);
        }
        if (row < bounds.Bottom - 1)
        {
            var hex = string.Join(' ', buffer.Take(8).Select(value => value.ToString("X2")));
            canvas.Write(bounds.X + 2, row, $"buf {hex}  {progress:D2}%", 3, bounds);
        }
    }

    private void Enqueue(string text, byte style)
    {
        jsonLines.Enqueue((text, style));
        while (jsonLines.Count > 48) jsonLines.Dequeue();
    }

    public void Dispose() => jsonLines.Clear();
}
