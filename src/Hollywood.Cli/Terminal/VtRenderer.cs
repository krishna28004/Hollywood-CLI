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
                (1, 1) => (0, 255, 170), (1, 2) => (20, 225, 255), (1, 3) => (255, 218, 55), (1, 4) => (255, 55, 205),
                (1, 5) => (255, 82, 105), (1, 6) => (135, 153, 180), (1, 7) => (190, 115, 255),
                (2, 1) => (57, 255, 110), (2, 2) => (35, 235, 220), (2, 3) => (255, 220, 75), (2, 4) => (255, 170, 64),
                (2, 5) => (255, 80, 100), (2, 6) => (164, 184, 198), (2, 7) => (225, 242, 255),
                (3, 1) => (0, 180, 60), (3, 2) => (45, 255, 95), (3, 3) => (140, 255, 150), (3, 4) => (0, 135, 50),
                (3, 5) => (255, 85, 85), (3, 6) => (90, 170, 105), (3, 7) => (205, 255, 210),
                (4, 1) => (205, 135, 35), (4, 2) => (255, 190, 60), (4, 3) => (255, 230, 145), (4, 4) => (255, 145, 80),
                (4, 5) => (255, 95, 75), (4, 6) => (180, 145, 95), (4, 7) => (255, 238, 205),
                (_, 1) => (0, 205, 85), (_, 2) => (50, 245, 110), (_, 3) => (255, 224, 75), (_, 4) => (80, 220, 150),
                (_, 5) => (255, 90, 90), (_, 6) => (154, 181, 166), (_, 7) => (220, 250, 230), _ => (0, 220, 100)
            };
            var weight = style is 1 or 2 or 5 ? "1;" : string.Empty;
            return $"\u001b[{weight}38;2;{rgb.Item1};{rgb.Item2};{rgb.Item3}m";
        }
        if (ColorMode == "16color") return style switch
        {
            1 => ThemeIndex switch { 1 => "\u001b[1;96m", 4 => "\u001b[1;33m", _ => "\u001b[1;92m" },
            2 => ThemeIndex switch { 1 => "\u001b[1;96m", 4 => "\u001b[93m", _ => "\u001b[1;96m" },
            3 => "\u001b[1;93m", 4 => "\u001b[1;33m", 5 => "\u001b[1;91m", 6 => "\u001b[37m", 7 => "\u001b[1;97m",
            _ => "\u001b[92m"
        };
        return style switch
        {
            1 => ThemeIndex switch { 1 => "\u001b[1;38;5;48m", 4 => "\u001b[1;38;5;214m", _ => "\u001b[1;92m" },
            2 => ThemeIndex switch { 1 => "\u001b[1;38;5;51m", 4 => "\u001b[38;5;220m", _ => "\u001b[1;96m" },
            3 => ThemeIndex == 1 ? "\u001b[1;38;5;220m" : "\u001b[1;93m",
            4 => ThemeIndex == 1 ? "\u001b[1;38;5;201m" : ThemeIndex == 4 ? "\u001b[38;5;208m" : "\u001b[93m",
            5 => "\u001b[1;91m", 6 => "\u001b[38;5;250m", 7 => "\u001b[1;97m",
            _ => "\u001b[92m"
        };
    }
}
