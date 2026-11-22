# Performance results

**No performance benchmark has been run.** There are no measured startup, first-frame, frame-time, input-latency, CPU, memory, allocation, throughput, resize, or soak results to report.

The interactive loop uses a centralized frame scheduler, caps frame rate from 1 to 60 FPS, and performs incremental cell diffs. These are implementation characteristics, not measured performance claims.

## Next benchmark

Record OS build, CPU, architecture, host, shell, viewport, runtime, publish mode, panel count, theme, frame limit, and capture method. Measure at least compact 80×24, reference 120×30, dense 160×45, and ten-panel scenarios, including constrained output. Drain the output stream continuously and bound captured bytes. Report median and p95 after warm-up, with at least 30 runs for launch metrics and a separate prolonged session for memory/CPU stability.
