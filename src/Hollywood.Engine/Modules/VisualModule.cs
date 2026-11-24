using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public interface IVisualModule : IDisposable
{
    string Title { get; }
    void Update(VisualUpdateContext context);
    void Render(Canvas canvas, Rect bounds, TimeSpan elapsed);
}

public readonly record struct VisualUpdateContext(TimeSpan Elapsed, DeterministicRandom Random, float Activity)
{
    public TimeSpan ScaleElapsed(double idleMultiplier, double maximumMultiplier)
    {
        var activity = Math.Clamp(Activity, 0f, 1f);
        var multiplier = idleMultiplier + (maximumMultiplier - idleMultiplier) * activity;
        var ticks = Math.Clamp((long)(Elapsed.Ticks * multiplier), 0L, TimeSpan.TicksPerSecond);
        return TimeSpan.FromTicks(ticks);
    }
}
