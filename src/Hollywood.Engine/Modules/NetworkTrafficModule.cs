using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class NetworkTrafficModule : IVisualModule
{
    private readonly Queue<string> events = new();
    private TimeSpan accumulated;
    private int packets;
    private static readonly string[] Protocols = ["TCP SYN/ACK", "TLS HELLO", "UDP DATA", "DNS QUERY"];
    public string Title => "NET TRAFFIC  /  SIM";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(700))
        {
            accumulated -= TimeSpan.FromMilliseconds(700);
            var source = $"192.0.2.{random.Next(254) + 1}";
            var target = $"198.51.100.{random.Next(254) + 1}";
            var protocol = Protocols[random.Next(Protocols.Length)].Replace(" SYN/ACK", "+A", StringComparison.Ordinal).Replace(" HELLO", "+H", StringComparison.Ordinal).Replace(" DATA", "+D", StringComparison.Ordinal).Replace(" QUERY", "+Q", StringComparison.Ordinal);
            events.Enqueue($"{source}>{target} {protocol}");
            while (events.Count > 24) events.Dequeue();
            packets++;
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 2);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 2, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 1, $"PACKETS {packets:D6}  LINK NOMINAL", 3, bounds);
        var row = bounds.Y + 3;
        foreach (var line in events.Reverse())
        {
            if (row >= bounds.Bottom - 1) break;
            canvas.Write(bounds.X + 2, row++, line, 1, bounds);
        }
    }

    public void Dispose() => events.Clear();
}
