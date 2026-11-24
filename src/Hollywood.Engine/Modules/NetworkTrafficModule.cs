using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class NetworkTrafficModule : IVisualModule
{
    private sealed record Packet(string Time, string Source, int SourcePort, string Destination, int DestinationPort, string Protocol, int Length, string Flags);

    private static readonly string[] Protocols = ["TCP", "TCP", "TLS", "UDP", "DNS"];
    private static readonly string[] Flags = ["SYN", "SYN,ACK", "ACK", "PSH,ACK", "FIN,ACK", "DATA"];
    private readonly Queue<Packet> packets = new();
    private TimeSpan accumulated;
    private int sequence;
    private int packetRate = 12_846;
    private int drops = 37;
    public string Title => "PACKET FLOW / RFC5737";

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.25, 4);
        var random = context.Random;
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(125))
        {
            accumulated -= TimeSpan.FromMilliseconds(125);
            var timestamp = new DateTime(2025, 1, 27, 14, 37, 19, DateTimeKind.Utc).AddMilliseconds(sequence * 83L);
            var source = $"192.0.2.{random.Next(240) + 10}";
            var destination = $"198.51.100.{random.Next(220) + 20}";
            var protocol = Protocols[random.Next(Protocols.Length)];
            var destinationPort = protocol switch { "TLS" => 443, "DNS" => 53, "TCP" => 22 + random.Next(3) * 80, _ => 8080 + random.Next(5) };
            packets.Enqueue(new Packet(timestamp.ToString("HH:mm:ss.fff"), source,
                32_000 + random.Next(30_000), destination, destinationPort, protocol,
                60 + random.Next(1450), Flags[random.Next(Flags.Length)]));
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
        canvas.Write(x, headerY, "UTC          SOURCE           SPORT DESTINATION      DPORT PROTO LEN  FLAGS    DECODE", 2, bounds);
        var row = headerY + 1;
        var rowLimit = bounds.Bottom - 1;
        foreach (var packet in packets.TakeLast(Math.Max(0, rowLimit - row)))
        {
            canvas.Write(x, row, packet.Time, 6, bounds);
            canvas.Write(x + 13, row, packet.Source.PadRight(16), 7, bounds);
            canvas.Write(x + 29, row, $"{packet.SourcePort,5}", 6, bounds);
            canvas.Write(x + 35, row, packet.Destination.PadRight(16), 2, bounds);
            canvas.Write(x + 51, row, $"{packet.DestinationPort,5}", 6, bounds);
            var protocolStyle = packet.Protocol is "TLS" or "DNS" ? (byte)5 : packet.Protocol == "UDP" ? (byte)4 : (byte)3;
            canvas.Write(x + 57, row, packet.Protocol.PadRight(5), protocolStyle, bounds);
            canvas.Write(x + 63, row, $"{packet.Length,4}", 0, bounds);
            canvas.Write(x + 69, row, packet.Flags.PadRight(9), packet.Flags.Contains("SYN", StringComparison.Ordinal) ? (byte)4 : (byte)1, bounds);
            var decode = packet.Protocol switch
            {
                "DNS" => "query A id=0x7a3c",
                "TLS" => "TLS1.3 app-data",
                "UDP" => $"datagram len={packet.Length}",
                _ => $"tcp seq={sequence - packets.Count + row % 997} ack win=64240"
            };
            canvas.Write(x + 79, row++, decode, 0, bounds);
        }
    }

    public void Dispose() => packets.Clear();
}
