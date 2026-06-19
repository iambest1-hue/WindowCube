using System.Text.Json.Serialization;

namespace WinLayout.Models;

public class UserConfig
{
    [JsonPropertyName("ModifierKey")]
    public string ModifierKey { get; set; } = "None";

    [JsonPropertyName("StackingKey")]
    public string StackingKey { get; set; } = "Ctrl";

    [JsonPropertyName("DragThreshold")]
    public int DragThreshold { get; set; } = 10;

    [JsonPropertyName("MinWindowSize")]
    public MinWindowSize MinWindowSize { get; set; } = new();

    [JsonPropertyName("HighlightOpacity")]
    public double HighlightOpacity { get; set; } = 0.35;

    [JsonPropertyName("DimOpacity")]
    public double DimOpacity { get; set; } = 0.5;

    [JsonPropertyName("RunAtStartup")]
    public bool RunAtStartup { get; set; }

    [JsonPropertyName("PerDesktopLayout")]
    public bool PerDesktopLayout { get; set; } = true;

    [JsonPropertyName("AutoApplyOnDesktopSwitch")]
    public bool AutoApplyOnDesktopSwitch { get; set; }

    [JsonPropertyName("ShowWindowButtons")]
    public bool ShowWindowButtons { get; set; } = true;

    [JsonPropertyName("MaxZoneButtonCount")]
    public int MaxZoneButtonCount { get; set; } = 3;

    [JsonPropertyName("BlacklistedProcesses")]
    public List<string> BlacklistedProcesses { get; set; } = new();

    /// <summary>控制窗口快捷按钮显示，不影响拖拽吸附黑名单</summary>
    [JsonPropertyName("WindowButtonsBlacklist")]
    public List<string> WindowButtonsBlacklist { get; set; } = new();

    [JsonPropertyName("ScreenLayouts")]
    public Dictionary<string, ScreenLayoutConfig> ScreenLayouts { get; set; } = new();

    [JsonPropertyName("WindowZoneMappings")]
    public Dictionary<string, Dictionary<string, WindowMapping>> WindowZoneMappings { get; set; } = new();
}

public class MinWindowSize
{
    [JsonPropertyName("Width")]
    public int Width { get; set; } = 200;

    [JsonPropertyName("Height")]
    public int Height { get; set; } = 150;
}

public class ScreenLayoutConfig
{
    [JsonPropertyName("ActiveLayoutId")]
    public string ActiveLayoutId { get; set; } = string.Empty;

    [JsonPropertyName("FavoriteLayoutIds")]
    public List<string> FavoriteLayoutIds { get; set; } = new();

    [JsonPropertyName("DesktopLayouts")]
    public Dictionary<string, string> DesktopLayouts { get; set; } = new();
}

public class WindowMapping
{
    [JsonPropertyName("ProcessName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("WindowTitle")]
    public string WindowTitle { get; set; } = string.Empty;
}
