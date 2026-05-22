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
        new("3×3 网格", CreateThreeByThree()),
        new("3×2 网格", CreateThreeByTwo()),
        new("4×2 网格", CreateFourByTwo()),
        new("主窗+右侧两小窗", CreateMainWithTwoRight()),
        new("主窗+底部两小窗", CreateMainWithTwoBottom()),
        new("五等分", CreateFiveColumn()),
        new("六等分", CreateSixColumn()),
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

    private static List<ZoneDefinition> CreateThreeByThree() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 2, Left = 1.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 3, Left = 2.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 4, Left = 0.0, Top = 1.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 5, Left = 1.0 / 3, Top = 1.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 6, Left = 2.0 / 3, Top = 1.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 7, Left = 0.0, Top = 2.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 8, Left = 1.0 / 3, Top = 2.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
        new() { Index = 9, Left = 2.0 / 3, Top = 2.0 / 3, Width = 1.0 / 3, Height = 1.0 / 3 },
    };

    private static List<ZoneDefinition> CreateThreeByTwo() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0 / 3, Height = 0.5 },
        new() { Index = 2, Left = 1.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 0.5 },
        new() { Index = 3, Left = 2.0 / 3, Top = 0.0, Width = 1.0 / 3, Height = 0.5 },
        new() { Index = 4, Left = 0.0, Top = 0.5, Width = 1.0 / 3, Height = 0.5 },
        new() { Index = 5, Left = 1.0 / 3, Top = 0.5, Width = 1.0 / 3, Height = 0.5 },
        new() { Index = 6, Left = 2.0 / 3, Top = 0.5, Width = 1.0 / 3, Height = 0.5 },
    };

    private static List<ZoneDefinition> CreateFourByTwo() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.25, Height = 0.5 },
        new() { Index = 2, Left = 0.25, Top = 0.0, Width = 0.25, Height = 0.5 },
        new() { Index = 3, Left = 0.5, Top = 0.0, Width = 0.25, Height = 0.5 },
        new() { Index = 4, Left = 0.75, Top = 0.0, Width = 0.25, Height = 0.5 },
        new() { Index = 5, Left = 0.0, Top = 0.5, Width = 0.25, Height = 0.5 },
        new() { Index = 6, Left = 0.25, Top = 0.5, Width = 0.25, Height = 0.5 },
        new() { Index = 7, Left = 0.5, Top = 0.5, Width = 0.25, Height = 0.5 },
        new() { Index = 8, Left = 0.75, Top = 0.5, Width = 0.25, Height = 0.5 },
    };

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

    private static List<ZoneDefinition> CreateFiveColumn() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 0.2, Height = 1.0 },
        new() { Index = 2, Left = 0.2, Top = 0.0, Width = 0.2, Height = 1.0 },
        new() { Index = 3, Left = 0.4, Top = 0.0, Width = 0.2, Height = 1.0 },
        new() { Index = 4, Left = 0.6, Top = 0.0, Width = 0.2, Height = 1.0 },
        new() { Index = 5, Left = 0.8, Top = 0.0, Width = 0.2, Height = 1.0 },
    };

    private static List<ZoneDefinition> CreateSixColumn() => new()
    {
        new() { Index = 1, Left = 0.0, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
        new() { Index = 2, Left = 1.0 / 6, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
        new() { Index = 3, Left = 2.0 / 6, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
        new() { Index = 4, Left = 3.0 / 6, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
        new() { Index = 5, Left = 4.0 / 6, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
        new() { Index = 6, Left = 5.0 / 6, Top = 0.0, Width = 1.0 / 6, Height = 1.0 },
    };
}

public record PresetTemplate(string Name, List<ZoneDefinition> Zones);
