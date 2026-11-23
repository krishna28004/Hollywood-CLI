using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class SecurityFeedModule : IVisualModule
{
    private static readonly string[] Events =
    [
        "apparmor=DENIED profile=fixture-worker op=open",
        "conntrack NEW 192.0.2.17>198.51.100.23",
        "systemd[1]: Started collector.service",
        "eth0: ingress fixture 100Mbit burst=128k",
        "audit: allow subject=analyzer-03 result=ok",
        "tls: rotated TLS_AES_128_GCM_SHA256",
        "udev: eth0 link state up (fixture)",
        "kernel: queue stable dropped=0",
        "WARN: sandbox threshold; alert routed",
        "netfilter: fixture packets=483721 bytes=297M",
        "loop: fixture device read-only after replay",
        "security: baseline refresh rules=128 delta=+3"
    ];

    private readonly Queue<(string Text, byte Style)> entries = new();
    private TimeSpan accumulated;
    private double timestamp = 86_542.9176;
    private int sequence;
    private int pendingLines;
    public string Title => "KERNEL EVENTS  [dmesg -w]";

    public SecurityFeedModule()
    {
        for (var index = 0; index < 10; index++) AddEntry(Events[index], (byte)(index == 0 || index == 8 ? 5 : index == 1 ? 2 : 0));
    }

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(155))
        {
            accumulated -= TimeSpan.FromMilliseconds(155);
            timestamp += 0.027 + random.Next(180) / 1000d;
            var message = Events[random.Next(Events.Length)];
            var style = message.Contains("WARN", StringComparison.Ordinal) || message.Contains("DENIED", StringComparison.Ordinal)
                ? (byte)4
                : message.StartsWith("nf_", StringComparison.Ordinal) || message.StartsWith("net", StringComparison.Ordinal) ? (byte)2 : (byte)0;
            AddEntry(message, style);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, $"{Title}  [{sequence:D2} events, +{pendingLines} new]", 1, bounds);
        var row = bounds.Y + 1;
        var end = bounds.Bottom - 1;
        foreach (var entry in entries)
        {
            if (row >= end) break;
            canvas.Write(bounds.X + 2, row++, entry.Text, entry.Style, bounds);
        }
        pendingLines = 0;
    }

    private void AddEntry(string message, byte style)
    {
        entries.Enqueue(($"[{timestamp,10:0.0000}] {message}", style));
        while (entries.Count > 48) entries.Dequeue();
        sequence++;
        pendingLines++;
    }

    public void Dispose() => entries.Clear();
}
