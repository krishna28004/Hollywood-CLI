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
        if (count == 7 && width >= 120 && height >= 30) return SolveOperationsLayout(width, height);
        var contentHeight = height - 2;
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

    private static IReadOnlyList<Rect> SolveOperationsLayout(int width, int height)
    {
        var top = 1;
        var contentHeight = height - 2;
        var leftWidth = width * 48 / 100;
        var rightWidth = width - leftWidth;
        var topHeight = contentHeight * 60 / 100;
        var systemHeight = topHeight / 2;
        var graphHeight = topHeight - systemHeight;
        var packetHeight = Math.Max(5, contentHeight / 5);
        var packetTop = top + topHeight;
        var lowerTop = packetTop + packetHeight;
        var lowerHeight = contentHeight - topHeight - packetHeight;
        var lowerLeftWidth = rightWidth / 2;

        return new[]
        {
            new Rect(0, top, leftWidth, topHeight),
            new Rect(leftWidth, top, rightWidth, systemHeight),
            new Rect(leftWidth, top + systemHeight, rightWidth, graphHeight),
            new Rect(0, packetTop, width, packetHeight),
            new Rect(0, lowerTop, leftWidth, lowerHeight),
            new Rect(leftWidth, lowerTop, lowerLeftWidth, lowerHeight),
            new Rect(leftWidth + lowerLeftWidth, lowerTop, rightWidth - lowerLeftWidth, lowerHeight)
        };
    }
}
