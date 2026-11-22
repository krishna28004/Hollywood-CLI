using System.Text;
using Hollywood.Engine.Rendering;

namespace Hollywood.Cli.Terminal;

public sealed class VtRenderer
{
    private readonly TextWriter writer;
    private Cell[]? previous;
    private int width;
    private int height;
    public int ThemeIndex { get; set; } = 0;
    public string ColorMode { get; set; } = "auto";

    public VtRenderer(TextWriter? writer = null) => this.writer = writer ?? Console.Out;

    public void Invalidate() => previous = null;

    public void Write(Canvas canvas)
    {
        var current = canvas.CopyCells();
        var resized = width != canvas.Width || height != canvas.Height;
        var output = new StringBuilder(Math.Min(current.Length * 2, 48_000));
        if (resized) output.Append("\u001b[2J\u001b[H");
        byte activeStyle = byte.MaxValue;
        for (var y = 0; y < canvas.Height; y++)
        {
            var x = 0;
            while (x < canvas.Width)
            {
                var index = y * canvas.Width + x;
                if (y == canvas.Height - 1 && x == canvas.Width - 1) { x++; continue; }
                if (!resized && previous is not null && current[index] == previous[index]) { x++; continue; }
                var start = x;
                output.Append($"\u001b[{y + 1};{x + 1}H");
                while (x < canvas.Width)
                {
                    index = y * canvas.Width + x;
                    if (y == canvas.Height - 1 && x == canvas.Width - 1) break;
                    if (x > start && !resized && previous is not null && current[index] == previous[index]) break;
                    var cell = current[index];
                    if (activeStyle != cell.Style)
                    {
                        output.Append(StyleSequence(cell.Style));
                        activeStyle = cell.Style;
                    }
                    output.Append(cell.Character == '\0' ? ' ' : cell.Character);
                    x++;
                }
            }
        }
        if (output.Length > 0)
        {
            output.Append("\u001b[0m");
            writer.Write(output);
            writer.Flush();
        }
        previous = current;
        width = canvas.Width;
        height = canvas.Height;
    }

    private string StyleSequence(byte style)
    {
        if (style == 0 || ColorMode == "mono") return "\u001b[0m";
        if (ColorMode == "truecolor")
        {
            var rgb = (ThemeIndex, style) switch
            {
                (1, 1) => (0, 255, 150), (1, 2) => (0, 230, 255), (1, 3) => (255, 40, 210), (1, _) => (165, 80, 255),
                (2, 1) => (50, 190, 90), (2, 2) => (40, 210, 225), (2, 3) => (115, 245, 120), (2, _) => (255, 185, 40),
                (3, 1) => (0, 160, 50), (3, 2) => (35, 255, 75), (3, 3) => (105, 255, 115), (3, _) => (0, 130, 40),
                (4, 1) => (190, 105, 20), (4, 2) => (255, 170, 35), (4, 3) => (255, 225, 125), (4, _) => (210, 135, 25),
                (_, 1) => (0, 150, 45), (_, 2) => (45, 235, 75), (_, 3) => (0, 190, 55), _ => (0, 135, 40)
            };
            return $"\u001b[38;2;{rgb.Item1};{rgb.Item2};{rgb.Item3}m";
        }
        if (ColorMode == "16color") return ThemeIndex switch
        {
            1 => style switch { 1 => "\u001b[36m", 2 => "\u001b[96m", 3 => "\u001b[95m", _ => "\u001b[35m" },
            2 => style switch { 1 => "\u001b[32m", 2 => "\u001b[36m", 3 => "\u001b[92m", _ => "\u001b[33m" },
            3 => style switch { 1 => "\u001b[32m", 2 => "\u001b[92m", 3 => "\u001b[92m", _ => "\u001b[32m" },
            4 => style switch { 1 => "\u001b[33m", 2 => "\u001b[93m", 3 => "\u001b[97m", _ => "\u001b[33m" },
            _ => style switch { 1 => "\u001b[32m", 2 => "\u001b[92m", 3 => "\u001b[32m", _ => "\u001b[32m" }
        };
        return ThemeIndex switch
        {
            1 => style switch { 1 => "\u001b[38;5;48m", 2 => "\u001b[38;5;51m", 3 => "\u001b[38;5;201m", _ => "\u001b[38;5;135m" },
            2 => style switch { 1 => "\u001b[32m", 2 => "\u001b[36m", 3 => "\u001b[92m", _ => "\u001b[33m" },
            3 => style switch { 1 => "\u001b[32m", 2 => "\u001b[92m", 3 => "\u001b[92m", _ => "\u001b[32m" },
            4 => style switch { 1 => "\u001b[38;5;136m", 2 => "\u001b[38;5;214m", 3 => "\u001b[38;5;228m", _ => "\u001b[38;5;172m" },
            _ => style switch { 1 => "\u001b[32m", 2 => "\u001b[92m", 3 => "\u001b[32m", _ => "\u001b[32m" }
        };
    }
}
