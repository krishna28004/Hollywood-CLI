namespace Hollywood.Engine.Rendering;

public readonly record struct Cell(char Character, byte Style)
{
    public static Cell Blank => new(' ', 0);
}
