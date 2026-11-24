using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

/// <summary>A bounded, deterministic terminal radar composed entirely of synthetic targets.</summary>
public sealed class RadarModule : IVisualModule
{
    private enum TargetKind : byte { Node, Service, Threat }
    private readonly record struct Target(double X, double Y, TargetKind Kind, string Id, string Address);

    private static readonly Target[] Targets =
    [
        new(-0.57, -0.22, TargetKind.Node, "NODE-01", "192.0.2.17"),
        new(0.18, -0.52, TargetKind.Service, "SVC-443", "192.0.2.44"),
        new(0.56, 0.16, TargetKind.Threat, "SIG-07", "198.51.100.23"),
        new(-0.17, 0.55, TargetKind.Node, "NODE-04", "198.51.100.7"),
        new(0.67, -0.34, TargetKind.Service, "SVC-53", "192.0.2.91")
    ];

    private double sweepAngle;
    private double elapsedSeconds;
    private TimeSpan targetTimer;
    private int activeTarget;
    private bool flickerOn = true;
    private bool suspended;
    private int flowCount = 12_846;
    private int eventCount = 4_301;

    public string Title => "RADAR / THREAT SCOPE";
    public bool IsSuspended => suspended;

    public void Suspend() => suspended = true;
    public void Resume() => suspended = false;

    public void Update(VisualUpdateContext context)
    {
        var elapsed = context.ScaleElapsed(0.3, 3);
        var random = context.Random;
        if (suspended) return;

        elapsedSeconds = (elapsedSeconds + elapsed.TotalSeconds) % 1.2;
        sweepAngle = (sweepAngle + elapsed.TotalSeconds * Math.Tau / 6.5) % Math.Tau;
        targetTimer += elapsed;
        while (targetTimer >= TimeSpan.FromMilliseconds(680))
        {
            targetTimer -= TimeSpan.FromMilliseconds(680);
            activeTarget = random.Next(Targets.Length);
            flickerOn = random.Next(5) != 0;
            flowCount = Math.Clamp(flowCount + random.Next(181) - 90, 7_000, 31_000);
            eventCount = Math.Clamp(eventCount + random.Next(91) - 45, 1_000, 10_000);
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        var title = bounds.Width < 25 ? "RADAR / SCOPE" : Title;
        canvas.Write(bounds.X + 2, bounds.Y, title, 1,
            new Rect(bounds.X + 1, bounds.Y, Math.Max(0, bounds.Width - 2), 1));
        if (bounds.Width < 12 || bounds.Height < 6) return;

        var hasLegend = bounds.Width >= 30;
        var scopeHeight = Math.Min(9, bounds.Height - (hasLegend ? 2 : 3));
        scopeHeight = Math.Max(3, scopeHeight);
        var scopeWidth = Math.Min(27, Math.Min(2 * scopeHeight + 1, bounds.Width - (hasLegend ? 24 : 4)));
        scopeWidth = Math.Max(9, scopeWidth);
        var scopeX = bounds.X + 2;
        var scopeY = bounds.Y + 1;
        var centerX = scopeX + scopeWidth / 2;
        var centerY = scopeY + scopeHeight / 2;
        var radiusX = Math.Max(2.0, (scopeWidth - 1) / 2.0);
        var radiusY = Math.Max(1.0, (scopeHeight - 1) / 2.0);
        var inner = new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        var bearing = (int)Math.Round(sweepAngle * 180 / Math.PI) % 360;
        var visible = Targets.Count(IsVisible) + 1;
        var threats = Targets.Count(static target => target.Kind == TargetKind.Threat);
        var status = bounds.Width < 42
            ? $"R2.4K B{bearing:000} T{visible} H{threats}"
            : $"RNG 2.4km | BRG {bearing:000} | TGT {visible:00} | THR {threats:00} | {flowCount / 1000.0:0.0}k/s SYN";

        DrawScope(canvas, inner, scopeX, scopeY, scopeWidth, scopeHeight, centerX, centerY, radiusX, radiusY);
        DrawSweep(canvas, inner, centerX, centerY, radiusX, radiusY);
        DrawTargets(canvas, inner, centerX, centerY, radiusX, radiusY);
        canvas.Set(centerX, centerY, '@', 2);

        if (hasLegend) DrawLegend(canvas, inner, scopeX + scopeWidth + 2, scopeY, status);

        if (!hasLegend && bounds.Height > 6)
            canvas.Write(bounds.X + 2, bounds.Bottom - 2, status, 3, inner);
    }

    private void DrawScope(Canvas canvas, Rect clip, int x, int y, int width, int height,
        int centerX, int centerY, double radiusX, double radiusY)
    {
        var tolerance = Math.Max(0.08, 1.15 / radiusX);
        for (var row = 0; row < height; row++)
        for (var column = 0; column < width; column++)
        {
            var dx = (column - radiusX) / radiusX;
            var dy = (row - radiusY) / radiusY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 1.02) continue;

            var onOuter = Math.Abs(distance - 0.96) <= tolerance;
            var onMiddle = Math.Abs(distance - 0.62) <= tolerance;
            var onInner = Math.Abs(distance - 0.30) <= tolerance;
            var isAxis = x + column == centerX || y + row == centerY;
            if (!onOuter && !onMiddle && !onInner && !isAxis) continue;

            var glyph = isAxis ? (column == (int)Math.Round(radiusX) ? '|' : '-') : '.';
            var color = isAxis ? (byte)2 : onOuter ? (byte)1 : (byte)6;
            SetInside(canvas, clip, x + column, y + row, glyph, color);
        }
        SetInside(canvas, clip, centerX, centerY, '+', 3);
    }

    private void DrawTargets(Canvas canvas, Rect clip, int centerX, int centerY, double radiusX, double radiusY)
    {
        for (var index = 0; index < Targets.Length; index++)
        {
            var target = Targets[index];
            if (!IsVisible(target)) continue;

            var x = (int)Math.Round(centerX + target.X * radiusX);
            var y = (int)Math.Round(centerY + target.Y * radiusY);
            var angle = Math.Atan2(target.Y, target.X);
            var nearSweep = Math.Abs(Math.IEEERemainder(angle - sweepAngle, Math.Tau)) < 0.70;
            var glyph = target.Kind switch
            {
                TargetKind.Node => 'o',
                TargetKind.Service => 's',
                TargetKind.Threat => '!',
                _ => '?'
            };
            var color = target.Kind switch
            {
                TargetKind.Node => (byte)1,
                TargetKind.Service => (byte)2,
                TargetKind.Threat => nearSweep ? (byte)5 : (byte)3,
                _ => (byte)6
            };
            if (index == activeTarget) glyph = target.Kind == TargetKind.Threat ? 'X' : '*';
            SetInside(canvas, clip, x, y, glyph, color);
        }
    }

    private void DrawSweep(Canvas canvas, Rect clip, int centerX, int centerY, double radiusX, double radiusY)
    {
        var endX = centerX + Math.Cos(sweepAngle) * radiusX;
        var endY = centerY + Math.Sin(sweepAngle) * radiusY;
        var steps = Math.Max(12, (int)Math.Ceiling(Math.Max(radiusX, radiusY) * 2));
        for (var step = 1; step <= steps; step++)
        {
            var progress = step / (double)steps;
            var x = (int)Math.Round(centerX + (endX - centerX) * progress);
            var y = (int)Math.Round(centerY + (endY - centerY) * progress);
            SetInside(canvas, clip, x, y, progress > 0.9 ? '*' : '.', 1);
        }
    }

    private void DrawLegend(Canvas canvas, Rect clip, int x, int y, string status)
    {
        canvas.Set(x - 1, y, '|', 6);
        var availableRows = clip.Bottom - y;
        if (availableRows <= 6)
        {
            canvas.Write(x, y, "LEGEND / SIM", 6, clip);
            canvas.Write(x, y + 1, "@ SELF  o NODE", 1, clip);
            canvas.Write(x, y + 2, "s SVC  ! THREAT", 5, clip);
            canvas.Write(x, y + availableRows - 1, status, 3, clip);
            return;
        }

        canvas.Write(x, y, "LEGEND / SIM", 6, clip);
        canvas.Write(x, y + 1, "@ SELF", 2, clip);
        canvas.Write(x, y + 2, "o NODE", 1, clip);
        canvas.Write(x, y + 3, "s SERVICE", 2, clip);
        canvas.Write(x, y + 4, "! THREAT", 5, clip);
        canvas.Write(x, y + 6, $"F {flowCount / 1000.0:0.0}k/s", 1, clip);
        canvas.Write(x, y + 7, $"E {eventCount / 1000.0:0.0}k/s", 2, clip);
    }

    private bool IsVisible(Target target) => target.Kind != TargetKind.Service || flickerOn || elapsedSeconds < 0.62;

    private static void SetInside(Canvas canvas, Rect clip, int x, int y, char glyph, byte style)
    {
        if (clip.Contains(x, y)) canvas.Set(x, y, glyph, style);
    }

    public void Dispose() { }
}
