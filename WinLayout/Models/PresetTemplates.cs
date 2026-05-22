namespace WinLayout.Models;

public static class PresetTemplates
{
    public static List<PresetTemplate> All { get; } = new()
    {
        new("左右二分", CreateLeftRightSplit()),
        new("上下二分", CreateTopBottomSplit()),
        new("左主右辅", CreateLeftMainRight()),
        new("三等分", CreateThreeColumn()),
        new("四等分", CreateFourGrid()),
        new("2×2 网格", CreateTwoByTwo()),
        new("主窗+右侧两小窗", CreateMainWithTwoRight()),
        new("主窗+底部两小窗", CreateMainWithTwoBottom()),
    };

    private static List<ZoneDefinition> CreateLeftRightSplit() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.5, Height = 1.0 },
        new() { Index = 2, Left = 0.5, Top = 0.0, Width = 0.5, Height = 1.0 },
    };

    private static List<ZoneDefinition> CreateTopBottomSplit() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0, Height = 0.5 },
        new() { Index = 2, Left = 0.0, Top = 0.5, Width = 1.0, Height = 0.5 },
    };

    private static List<ZoneDefinition> CreateLeftMainRight() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.7, Height = 1.0 },
        new() { Index = 2, Left = 0.7, Top = 0.0, Width = 0.3, Height = 1.0 },
    };

    private static List<ZoneDefinition> CreateThreeColumn() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0 / 3, Height = 1.0 },
        new() { Index = 2, Left = 1.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 1.0 },
        new() { Index = 3, Left = 2.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 1.0 },
    };

    private static List<ZoneDefinition> CreateFourGrid() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.5, Height = 0.5 },
        new() { Index = 2, Left = 0.5, Top = 0.0, Width = 0.5, Height = 0.5 },
        new() { Index = 3, Left = 0.0, Top = 0.5, Width = 0.5, Height = 0.5 },
        new() { Index = 4, Left = 0.5, Top = 0.5, Width = 0.5, Height = 0.5 },
    };

    private static List<ZoneDefinition> CreateTwoByTwo() => CreateFourGrid();

    private static List<ZoneDefinition> CreateMainWithTwoRight() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.6, Height = 1.0 },
        new() { Index = 2, Left = 0.6, Top = 0.0, Width = 0.4, Height = 0.5 },
        new() { Index = 3, Left = 0.6, Top = 0.5, Width = 0.4, Height = 0.5 },
    };

    private static List<ZoneDefinition> CreateMainWithTwoBottom() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0, Height = 0.6 },
        new() { Index = 2, Left = 0.0, Top = 0.6, Width = 0.5, Height = 0.4 },
        new() { Index = 3, Left = 0.5, Top = 0.6, Width = 0.5, Height = 0.4 },
    };
}

public record PresetTemplate(string Name, List<ZoneDefinition> Zones);
