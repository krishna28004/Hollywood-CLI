namespace Hollywood.Engine.Rendering;

public sealed class Canvas
{
    private readonly Cell[] cells;
    public int Width { get; }
    public int Height { get; }

    public Canvas(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 240 * 80)
            throw new ArgumentOutOfRangeException(nameof(width), "Canvas dimensions must fit within 240 by 80 cells.");
        Width = width;
        Height = height;
        cells = new Cell[width * height];
        Clear();
    }

    public Cell this[int x, int y] => cells[y * Width + x];
    public ReadOnlySpan<Cell> Row(int y) => cells.AsSpan(y * Width, Width);

    public void Clear()
    {
        Array.Fill(cells, Cell.Blank);
    }

    public void Set(int x, int y, char character, byte style = 0)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || char.IsControl(character))
            return;
        cells[y * Width + x] = new Cell(character, style);
    }

    public void Write(int x, int y, string text, byte style = 0, Rect? clip = null)
    {
        var bounds = clip ?? new Rect(0, 0, Width, Height);
        var targetX = x;
        foreach (var character in text)
        {
            if (character == '\n')
                break;
            if (bounds.Contains(targetX, y))
                Set(targetX, y, character, style);
            targetX++;
        }
    }

    public void Border(Rect rect, byte style = 0)
    {
        if (rect.Width < 2 || rect.Height < 2)
            return;
        for (var x = rect.X; x < rect.Right; x++)
        {
            Set(x, rect.Y, x == rect.X ? '+' : x == rect.Right - 1 ? '+' : '-', style);
            Set(x, rect.Bottom - 1, x == rect.X || x == rect.Right - 1 ? '+' : '-', style);
        }
        for (var y = rect.Y + 1; y < rect.Bottom - 1; y++)
        {
            Set(rect.X, y, '|', style);
            Set(rect.Right - 1, y, '|', style);
        }
    }

    public string ToPlainText()
    {
        var lines = new string[Height];
        for (var y = 0; y < Height; y++)
        {
            var chars = new char[Width];
            for (var x = 0; x < Width; x++) chars[x] = this[x, y].Character;
            lines[y] = new string(chars).TrimEnd();
        }
        return string.Join(Environment.NewLine, lines);
    }

    public Cell[] CopyCells() => (Cell[])cells.Clone();
}
