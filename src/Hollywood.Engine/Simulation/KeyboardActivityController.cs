namespace Hollywood.Engine.Simulation;

/// <summary>Measures recent key-event frequency without retaining key identities or text.</summary>
public sealed class KeyboardActivityController
{
    public const int MaximumEventsPerUpdate = 64;
    private const double ImpulsePerKey = 0.09;

    private readonly TimeProvider clock;
    private readonly float idleIntensity;
    private readonly float maximumIntensity;
    private readonly float sensitivity;
    private readonly double decayTimeConstantSeconds;
    private long lastTimestamp;
    private int pendingEvents;
    private double pressure;
    private bool enabled;
    private bool paused;

    public float Activity { get; private set; }
    public bool Enabled => enabled;
    public bool IsPaused => paused;

    public KeyboardActivityController(
        TimeProvider clock,
        bool enabled = true,
        float idleIntensity = 0.05f,
        float maximumIntensity = 1f,
        float sensitivity = 1f,
        double cooldownSeconds = 3d)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (!float.IsFinite(idleIntensity) || idleIntensity is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(idleIntensity));
        if (!float.IsFinite(maximumIntensity) || maximumIntensity < idleIntensity || maximumIntensity > 1) throw new ArgumentOutOfRangeException(nameof(maximumIntensity));
        if (!float.IsFinite(sensitivity) || sensitivity is < 0.1f or > 5f) throw new ArgumentOutOfRangeException(nameof(sensitivity));
        if (!double.IsFinite(cooldownSeconds) || cooldownSeconds is < 0.5 or > 10) throw new ArgumentOutOfRangeException(nameof(cooldownSeconds));

        this.idleIntensity = idleIntensity;
        this.maximumIntensity = maximumIntensity;
        this.sensitivity = sensitivity;
        decayTimeConstantSeconds = cooldownSeconds / 3d;
        this.enabled = enabled;
        lastTimestamp = clock.GetTimestamp();
        Activity = idleIntensity;
    }

    /// <summary>Records one anonymous logical key event; excess repeats in one tick are dropped.</summary>
    public void RecordKeyEvent()
    {
        if (enabled && !paused && pendingEvents < MaximumEventsPerUpdate)
            pendingEvents++;
    }

    public float Update()
    {
        var now = clock.GetTimestamp();
        var elapsed = clock.GetElapsedTime(lastTimestamp, now);
        lastTimestamp = now;
        var seconds = Math.Clamp(elapsed.TotalSeconds, 0d, 30d);

        if (!enabled || paused)
        {
            pressure = 0;
            pendingEvents = 0;
            Activity = idleIntensity;
            return Activity;
        }

        pressure *= Math.Exp(-seconds / decayTimeConstantSeconds);
        if (pendingEvents > 0)
            pressure += ImpulsePerKey * sensitivity * pendingEvents;
        pendingEvents = 0;
        pressure = Math.Clamp(pressure, 0d, 1d);
        Activity = idleIntensity + (maximumIntensity - idleIntensity) * (float)pressure;
        return Activity;
    }

    public void SetEnabled(bool value)
    {
        enabled = value;
        pressure = 0;
        pendingEvents = 0;
        Activity = idleIntensity;
    }

    public void SetPaused(bool value)
    {
        paused = value;
        pressure = 0;
        pendingEvents = 0;
        Activity = idleIntensity;
    }
}
