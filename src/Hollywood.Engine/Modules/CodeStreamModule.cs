using Hollywood.Engine.Rendering;
using Hollywood.Engine.Simulation;

namespace Hollywood.Engine.Modules;

public sealed class CodeStreamModule : IVisualModule
{
    private static readonly string[] Keywords = ["from", "import", "as", "class", "def", "async", "await", "for", "in", "if", "else", "return", "raise", "while", "use", "struct", "impl", "fn", "let", "mut", "pub", "internal", "using", "var", "new", "void", "int", "string", "param", "function", "foreach", "object", "true", "false", "null", "None"];
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
            RenderCodeLine(canvas, bounds.X + 7, y, text, bounds);
        }
    }

    // Token coloring is deliberately lightweight: the stream is illustrative, so a
    // small lexical pass gives it editor-like detail without a parser per language.
    private static void RenderCodeLine(Canvas canvas, int x, int y, string line, Rect clip)
    {
        var i = 0;
        var inString = false;
        var quote = '\0';
        while (i < line.Length && x + i < clip.Right - 1)
        {
            var start = i;
            var c = line[i];
            byte style;
            if (!inString && (c == '#' || (c == '/' && i + 1 < line.Length && line[i + 1] == '/')))
            {
                for (; i < line.Length && x + i < clip.Right - 1; i++) canvas.Set(x + i, y, line[i], 1);
                return;
            }
            if (c is '"' or '\'')
            {
                quote = c;
                inString = true;
                i++;
                while (i < line.Length)
                {
                    if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                    if (line[i++] == quote) { inString = false; break; }
                }
                style = 3;
            }
            else if (char.IsLetter(c) || c == '_' || c == '$')
            {
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '_' or '$')) i++;
                style = KeywordStyle(line.AsSpan(start, i - start));
            }
            else if (char.IsDigit(c))
            {
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '.' or '_')) i++;
                style = 4;
            }
            else
            {
                i++;
                style = c is '=' or '>' or '<' or '+' or '-' or '|' or '&' ? (byte)5
                    : c is '(' or ')' or '[' or ']' or '{' or '}' or ':' or ',' or ';' or '.' ? (byte)6
                    : (byte)0;
            }
            for (var column = start; column < i && x + column < clip.Right - 1; column++)
                canvas.Set(x + column, y, line[column], style);
        }
    }

    private static byte KeywordStyle(ReadOnlySpan<char> token)
    {
        foreach (var keyword in Keywords)
            if (token.SequenceEqual(keyword)) return 2;
        return token[0] is >= 'A' and <= 'Z' ? (byte)7 : (byte)0;
    }

    public void Dispose() { }
}
