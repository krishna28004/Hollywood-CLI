using Hollywood.Engine.Rendering;

namespace Hollywood.Engine.Layout;

public static class LayoutSolver
{
    public static IReadOnlyList<Rect> Solve(int width, int height, int count)
    {
        width = Math.Clamp(width, 1, 240);
        height = Math.Clamp(height, 1, 80);
        count = Math.Clamp(count, 1, 10);
        if (width < 20 || height < 7) return Array.Empty<Rect>();
        var contentHeight = height - 3;
        if (count == 1) return new[] { new Rect(0, 1, width, contentHeight) };
        var columns = count switch { 2 => 2, 3 => 2, 4 => 2, <= 6 => 3, _ => 4 };
        var rows = (count + columns - 1) / columns;
        var result = new List<Rect>(count);
        for (var index = 0; index < count; index++)
        {
            var row = index / columns;
            var column = index % columns;
            var rowStart = 1 + row * contentHeight / rows;
            var rowEnd = 1 + (row + 1) * contentHeight / rows;
            var colStart = column * width / columns;
            var colEnd = (column + 1) * width / columns;
            var remaining = Math.Min(columns, count - row * columns);
            if (column >= remaining) break;
            result.Add(new Rect(colStart, rowStart, colEnd - colStart, rowEnd - rowStart));
        }
        return result;
    }
}
