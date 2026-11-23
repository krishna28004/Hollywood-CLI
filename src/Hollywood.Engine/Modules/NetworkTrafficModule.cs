using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class NetworkTrafficModule : IVisualModule
{
    private sealed record Packet(string Time, string Source, string Destination, string Protocol, int Length, string Flags);

    private static readonly string[] Protocols = ["TCP", "TCP", "TLS", "UDP", "DNS"];
    private static readonly string[] Flags = ["SYN", "SYN,ACK", "ACK", "PSH,ACK", "FIN,ACK", "DATA"];
    private readonly Queue<Packet> packets = new();
    private TimeSpan accumulated;
    private int sequence;
    private int packetRate = 12_846;
    private int drops = 37;
    public string Title => "PACKET FLOW / RFC5737";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(125))
        {
            accumulated -= TimeSpan.FromMilliseconds(125);
            var timestamp = new DateTime(2025, 1, 27, 14, 37, 19, DateTimeKind.Utc).AddMilliseconds(sequence * 83L);
            var source = $"192.0.2.{random.Next(240) + 10}";
            var destination = $"198.51.100.{random.Next(220) + 20}";
            packets.Enqueue(new Packet(timestamp.ToString("HH:mm:ss.fff"), source, destination,
                Protocols[random.Next(Protocols.Length)], 60 + random.Next(1450), Flags[random.Next(Flags.Length)]));
            while (packets.Count > 48) packets.Dequeue();
            sequence++;
            packetRate = Math.Clamp(packetRate + random.Next(501) - 250, 8_000, 27_000);
            drops = Math.Clamp(drops + random.Next(5) - 2, 0, 240);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        if (bounds.Height < 3) return;
        canvas.Write(bounds.X + 2, bounds.Y + 1, $"pps {packetRate:N0}  drops {drops}  eth0-fixture", 0, bounds);

        var x = bounds.X + 1;
        var headerY = bounds.Y + 2;
        canvas.Write(x, headerY, "UTC          SOURCE          DESTINATION      PROTO LEN", 2, bounds);
        var row = headerY + 1;
        var rowLimit = bounds.Bottom - 1;
        foreach (var packet in packets.TakeLast(Math.Max(0, rowLimit - row)))
        {
            canvas.Write(x, row, packet.Time.PadRight(13), 0, bounds);
            canvas.Write(x + 13, row, packet.Source.PadRight(16), 7, bounds);
            canvas.Write(x + 29, row, packet.Destination.PadRight(16), 2, bounds);
            canvas.Write(x + 45, row, packet.Protocol.PadRight(5), packet.Protocol is "TLS" or "DNS" ? (byte)5 : (byte)3, bounds);
            canvas.Write(x + 50, row++, $"{packet.Length,4}", 0, bounds);
        }
    }

    public void Dispose() => packets.Clear();
}
