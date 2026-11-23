using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class SystemMetricsModule : IVisualModule
{
    private static readonly string[] Processes =
    [
        "python3 collector.py", "analyzer-worker", "systemd-journald", "netflowd --fixture",
        "sshd: ubuntu@pts/1", "node event-pipeline.js", "redis-server *:6379", "dotnet Hollywood.Engine"
    ];

    private int cpu = 13;
    private int memory = 68;
    private int swap = 7;
    private int tick;
    private TimeSpan accumulated;
    private TimeSpan uptime;
    public string Title => "SYSTEM LOAD";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        uptime += elapsed;
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(240))
        {
            accumulated -= TimeSpan.FromMilliseconds(240);
            cpu = Math.Clamp(cpu + random.Next(13) - 6, 3, 92);
            memory = Math.Clamp(memory + random.Next(5) - 2, 52, 79);
            swap = Math.Clamp(swap + random.Next(3) - 1, 3, 13);
            tick++;
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        if (bounds.Height < 5) return;

        DrawMeter(canvas, bounds, bounds.Y + 1, "CPU", cpu, 3);
        DrawMeter(canvas, bounds, bounds.Y + 2, "MEM", memory, 5);
        DrawMeter(canvas, bounds, bounds.Y + 3, "SWP", swap, 4);
        if (bounds.Height < 7) return;

        var up = $"{(int)uptime.TotalDays:D1}d {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        canvas.Write(bounds.X + 2, bounds.Y + 4, $"T 214 thr 620  L {0.71 + cpu / 100d:0.00} {0.69 + cpu / 100d:0.00} 1.32  up {up}", 0, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 5, "PID USER PRI NI VIRT RES S CPU% MEM% COMMAND", 2, bounds);

        var availableRows = Math.Max(0, bounds.Height - 7);
        for (var row = 0; row < availableRows; row++)
        {
            var processIndex = (tick / 2 + row) % Processes.Length;
            var processCpu = Math.Max(0.1, (cpu * (0.08 + ((processIndex * 13) % 19) / 100d)));
            var processMemory = 0.4 + processIndex * 0.23;
            var pid = 2371 + processIndex * 317;
            var lineY = bounds.Y + 6 + row;
            var x = bounds.X + 2;
            canvas.Write(x, lineY, $"{pid,4}", 6, bounds);
            canvas.Write(x + 5, lineY, "ubuntu", 0, bounds);
            canvas.Write(x + 12, lineY, "20  0", 6, bounds);
            canvas.Write(x + 19, lineY, $"{128 + processIndex * 31,3}M", 0, bounds);
            canvas.Write(x + 24, lineY, $"{48 + processIndex * 17,3}M", 0, bounds);
            canvas.Write(x + 29, lineY, processIndex == 0 ? "R" : "S", processIndex == 0 ? (byte)3 : (byte)6, bounds);
            canvas.Write(x + 32, lineY, $"{processCpu,4:0.0}", processCpu >= 5 ? (byte)5 : (byte)1, bounds);
            canvas.Write(x + 37, lineY, $"{processMemory,4:0.0}", 4, bounds);
            canvas.Write(x + 42, lineY, Processes[processIndex], processIndex == 0 ? (byte)7 : (byte)0, bounds);
        }
    }

    private static void DrawMeter(Canvas canvas, Rect bounds, int row, string label, int percent, byte style)
    {
        var width = Math.Clamp(bounds.Width - 27, 4, 30);
        var filled = width * percent / 100;
        canvas.Write(bounds.X + 2, row, $"{label,-3} [{new string('|', filled)}{new string('.', width - filled)}] {percent,3}%", style, bounds);
    }

    public void Dispose() { }
}
