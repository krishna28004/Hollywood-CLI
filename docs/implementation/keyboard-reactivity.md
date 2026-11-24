# Keyboard-reactive animation

## Behavior

Hollywood measures only the count of logical key events delivered by its own interactive terminal adapter. Key identities and character contents are not retained, logged, or transmitted. At most 64 events are consumed per render-loop iteration. Typing raises a normalized activity signal; a monotonic `TimeProvider` clock exponentially decays it after input stops. The default idle level is 0.05, maximum is 1.0, sensitivity is 1.0, and the decay time constant is one second (three seconds is the approximate 95% cooldown).

The central scheduler passes activity to every visual module. Module update elapsed time is scaled independently from the terminal presentation rate. Idle frames are capped at 6 FPS; active frames use the configured cap, up to 60 FPS. Reduced-motion clamps activity and slows simulation updates. Pausing suppresses module updates. Real telemetry, if added later, must not consume this simulation activity signal.

Shortcuts: Q or Escape exits; Ctrl+C cancels; Space pauses/resumes; T cycles themes; L cycles focus/auto/dense layouts. Other recognized keys only raise activity. `--keyboard-reactive on|off` overrides the version 1 JSON `keyboardReactive` object.

## Implementation

- `KeyboardActivityController` keeps only bounded pending event count and smoothed pressure.
- `VisualUpdateContext` carries shared elapsed time, deterministic PRNG, and normalized activity.
- All eleven built-in modules scale their own update cadence within fixed bounds.
- POSIX input handles Linux and Darwin separately, parses common ANSI cursor/function keys, counts UTF-8 code points once, and retains ISIG for Ctrl+C.
- Windows input filters control/alt chords and modifier-only records; repeat events are bounded by the scheduler input cap.

## Verification evidence

- `dotnet build Hollywood.sln -c Release -m:1`: passed, zero warnings and errors.
- `tests/Hollywood.UnitTests`: passed, including injected-clock idle, isolated input, rapid input, smooth cooldown, paused/disabled states, invalid configuration, all module layouts, radar determinism, and rendering bounds.
- `tests/Hollywood.ConPty` against the Windows x64 apphost: passed scene/animation, typing activity, Space pause/resume, resize, Q/Escape exit, Ctrl+C exit code 130, and cursor restoration.
- These checks exercise Windows ConPTY, not visible Windows Terminal/Console Host. Linux/macOS PTY and real terminal emulator checks remain pending. CPU and memory benchmarks have not yet been collected.

## Current limits

Escape is ambiguous with terminal escape sequences, so the POSIX adapter waits briefly to distinguish a standalone Escape from an ANSI key sequence. Terminal protocols outside the recognized CSI/SS3 subset are ignored. Activity currently adjusts synthetic module update cadence and their elapsed-time-driven animation; it does not imply real machine or network load.
