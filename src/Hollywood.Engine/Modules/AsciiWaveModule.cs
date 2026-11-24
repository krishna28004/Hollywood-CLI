using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class AsciiWaveModule : IVisualModule
{
    private readonly int[] receive = new int[96];
    private readonly int[] transmit = new int[96];
    private TimeSpan accumulated;
    private int rxRate = 18;
    private int txRate = 4;
    public string Title => "RX/TX THROUGHPUT (eth0)";

    public AsciiWaveModule()
    {
        for (var index = 0; index < receive.Length; index++)
        {
            receive[index] = 34 + (int)(Math.Sin(index * 0.47) * 15) + index % 13;
            transmit[index] = 15 + (int)(Math.Sin(index * 0.31 + 0.9) * 8) + index % 7;
        }
    }

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.3, 2.7);
        var random = context.Random;
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(110))
        {
            accumulated -= TimeSpan.FromMilliseconds(110);
            Array.Copy(receive, 1, receive, 0, receive.Length - 1);
            Array.Copy(transmit, 1, transmit, 0, transmit.Length - 1);
            var lastRx = receive[^2];
            var lastTx = transmit[^2];
            receive[^1] = Math.Clamp(lastRx + random.Next(17) - 8, 16, 92);
            transmit[^1] = Math.Clamp(lastTx + random.Next(13) - 6, 5, 56);
            rxRate = Math.Clamp((receive[^1] * 43 + 499) / 100, 3, 39);
            txRate = Math.Clamp((transmit[^1] * 18 + 499) / 100, 1, 11);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        canvas.Write(bounds.X + 2, bounds.Y + 1, $"RX {rxRate,2}.{rxRate % 10} MiB/s", 1, bounds);
        canvas.Write(bounds.X + 25, bounds.Y + 1, $"TX {txRate,2}.{txRate % 10} MiB/s  [SIMULATED CAPTURE]", 2, bounds);
        var graphTop = bounds.Y + 2;
        var graphBottom = bounds.Bottom - 2;
        if (graphBottom <= graphTop) return;
        var graphHeight = graphBottom - graphTop;
        var graphLeft = bounds.X + 6;
        var graphWidth = Math.Max(0, bounds.Width - 9);
        canvas.Write(bounds.X + 1, graphTop, "32M", 0, bounds);
        if (graphHeight > 3) canvas.Write(bounds.X + 1, graphTop + graphHeight / 2, "16M", 0, bounds);
        canvas.Write(bounds.X + 2, graphBottom, "0", 0, bounds);

        for (var gridY = graphTop + 2; gridY < graphBottom; gridY += 4)
            for (var gridX = graphLeft; gridX < bounds.Right - 2; gridX += 2)
                canvas.Set(gridX, gridY, '.', 6);

        var previousRxY = -1;
        var previousTxY = -1;
        for (var column = 0; column < graphWidth; column++)
        {
            var sample = column * (receive.Length - 1) / Math.Max(1, graphWidth - 1);
            var rxY = graphBottom - 1 - receive[sample] * Math.Max(1, graphHeight - 1) / 100;
            var txY = graphBottom - 1 - transmit[sample] * Math.Max(1, graphHeight - 1) / 100;
            if (previousRxY >= 0 && Math.Abs(rxY - previousRxY) > 1)
                for (var y = Math.Min(rxY, previousRxY) + 1; y < Math.Max(rxY, previousRxY); y++) canvas.Set(graphLeft + column, y, ':', 1);
            if (previousTxY >= 0 && Math.Abs(txY - previousTxY) > 1)
                for (var y = Math.Min(txY, previousTxY) + 1; y < Math.Max(txY, previousTxY); y++) canvas.Set(graphLeft + column, y, ':', 2);
            canvas.Set(graphLeft + column, rxY, '*', 1);
            canvas.Set(graphLeft + column, txY, '.', 2);
            previousRxY = rxY;
            previousTxY = txY;
        }

        canvas.Write(bounds.Right - 14, graphTop, "* RX MiB/s", 1, bounds);
        canvas.Write(bounds.Right - 14, graphTop + 1, ". TX MiB/s", 2, bounds);
    }

    public void Dispose() { }
}
