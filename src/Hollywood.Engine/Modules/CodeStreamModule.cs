using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class CodeStreamModule : IVisualModule
{
    private static readonly (string File, string[] Lines)[] Listings =
    [
        ("src/collector/flow_parser.py", [
            "from dataclasses import dataclass", "from ipaddress import ip_address", "from typing import Iterable",
            "", "@dataclass(frozen=True)", "class FlowRecord:", "    source: str", "    destination: str",
            "    protocol: str", "    byte_count: int", "", "def normalize_record(raw: dict) -> FlowRecord:",
            "    source = ip_address(raw[\"src\"]) ", "    target = ip_address(raw[\"dst\"]) ",
            "    if not source.is_global and not target.is_global:", "        return FlowRecord(",
            "            source=str(source),", "            destination=str(target),", "            protocol=raw[\"proto\"],",
            "            byte_count=int(raw.get(\"bytes\", 0)),", "        )", "    raise ValueError(\"outside fixture range\")",
            "", "async def consume(stream: Iterable[dict]) -> None:", "    async for item in stream:",
            "        record = normalize_record(item)", "        await pipeline.submit(record)", "        counters.flows += 1",
            "        counters.bytes += record.byte_count", "        await metrics.flush_if_due()"
        ]),
        ("src/engine/FrameScheduler.cs", [
            "using System.Diagnostics;", "using System.Threading.Channels;", "", "internal sealed class FrameScheduler",
            "{", "    private readonly Channel<FrameEvent> events;", "    private readonly TimeProvider clock;",
            "    private long frameNumber;", "", "    public async ValueTask RunAsync(CancellationToken stop)", "    {",
            "        var timer = new PeriodicTimer(frameInterval, clock);", "        while (await timer.WaitForNextTickAsync(stop))",
            "        {", "            var started = clock.GetTimestamp();", "            var changes = await events.Reader.ReadBatchAsync(stop);",
            "            renderer.Apply(changes);", "            await terminal.WriteFrameAsync(renderer.Diff());", "            frameNumber++;",
            "            metrics.Record(clock.GetElapsedTime(started));", "        }", "    }", "}",
            "", "// Synthetic events only; no network or process control is attached."
        ]),
        ("src/stream/packet_window.rs", [
            "use std::collections::VecDeque;", "use std::time::{Duration, Instant};", "", "#[derive(Clone, Copy)]",
            "struct Sample { rx: u32, tx: u32, at: Instant }", "", "struct Window {", "    samples: VecDeque<Sample>,",
            "    capacity: usize,", "}", "", "impl Window {", "    fn push(&mut self, sample: Sample) {",
            "        self.samples.push_back(sample);", "        while self.samples.len() > self.capacity {",
            "            self.samples.pop_front();", "        }", "    }", "", "    fn rate(&self, span: Duration) -> (u64, u64) {",
            "        let first = self.samples.front()?;", "        let last = self.samples.back()?;", "        let elapsed = last.at - first.at;",
            "        scale_bytes(first, last, elapsed.max(span))", "    }", "}", "", "// Fixture stream: RFC 5737 addresses only."
        ]),
        ("scripts/telemetry.ps1", [
            "[CmdletBinding()]", "param([int] $SampleCount = 64)", "", "$ErrorActionPreference = 'Stop'",
            "$window = [System.Collections.Generic.Queue[object]]::new()", "", "function Add-Sample {",
            "    param([datetime] $Timestamp, [double] $Load)", "    $entry = [pscustomobject]@{",
            "        Timestamp = $Timestamp.ToUniversalTime()", "        Load = [math]::Round($Load, 2)",
            "        Source = 'synthetic-fixture'", "    }", "    $window.Enqueue($entry)",
            "    while ($window.Count -gt $SampleCount) {", "        $null = $window.Dequeue()", "    }",
            "}", "", "1..$SampleCount | ForEach-Object {", "    Add-Sample (Get-Date) (Get-Random -Minimum .1 -Maximum 1.8)",
            "    Start-Sleep -Milliseconds 80", "}", "", "$window.ToArray() | ConvertTo-Json -Depth 4"
        ])
    ];

    private TimeSpan accumulated;
    private int listingIndex;
    private int scrollOffset;
    private int linesRendered;

    public string Title => $"CODE / {Path.GetFileName(Listings[listingIndex].File)}";

    public void Update(TimeSpan elapsed, DeterministicRandom random)
    {
        accumulated += elapsed;
        while (accumulated >= TimeSpan.FromMilliseconds(480))
        {
            accumulated -= TimeSpan.FromMilliseconds(480);
            scrollOffset = (scrollOffset + 1) % Listings[listingIndex].Lines.Length;
            linesRendered++;
            if (linesRendered > Listings[listingIndex].Lines.Length * 3)
            {
                listingIndex = (listingIndex + 1) % Listings.Length;
                scrollOffset = 0;
                linesRendered = 0;
            }
        }
    }

    public void Render(Canvas canvas, Rect bounds, TimeSpan elapsed)
    {
        canvas.Border(bounds, 6);
        canvas.Write(bounds.X + 2, bounds.Y, Title, 1, bounds);
        var source = Listings[listingIndex].Lines;
        var visibleRows = Math.Max(0, bounds.Height - 2);
        for (var row = 0; row < visibleRows; row++)
        {
            var sourceIndex = (scrollOffset + row) % source.Length;
            var text = source[sourceIndex];
            var y = bounds.Y + 1 + row;
            var current = row == visibleRows - 1;
            canvas.Write(bounds.X + 1, y, current ? ">" : " ", 3, bounds);
            canvas.Write(bounds.X + 2, y, $"{sourceIndex + 1,3} ", 6, bounds);
            canvas.Write(bounds.X + 7, y, text, StyleFor(text), bounds);
        }
    }

    private static byte StyleFor(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal)) return 1;
        if (trimmed.StartsWith("def ", StringComparison.Ordinal) || trimmed.StartsWith("class ", StringComparison.Ordinal)
            || trimmed.StartsWith("async ", StringComparison.Ordinal) || trimmed.StartsWith("fn ", StringComparison.Ordinal)
            || trimmed.StartsWith("function ", StringComparison.Ordinal) || trimmed.StartsWith("internal ", StringComparison.Ordinal)
            || trimmed.StartsWith("use ", StringComparison.Ordinal) || trimmed.StartsWith("using ", StringComparison.Ordinal)) return 2;
        if (line.Contains('"') || line.Contains('\'')) return 3;
        if (trimmed.StartsWith("return ", StringComparison.Ordinal) || trimmed.StartsWith("raise ", StringComparison.Ordinal)
            || trimmed.StartsWith("if ", StringComparison.Ordinal) || trimmed.StartsWith("while ", StringComparison.Ordinal)) return 4;
        return 0;
    }

    public void Dispose() { }
}
