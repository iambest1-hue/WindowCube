using System.Text.Json.Serialization;

namespace WinLayout.Models;

public class LayoutDefinition
{
    [JsonPropertyName("LayoutId")]
    public string LayoutId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ScreenId")]
    public string ScreenId { get; set; } = string.Empty;

    [JsonPropertyName("Zones")]
    public List<ZoneDefinition> Zones { get; set; } = new();

    [JsonPropertyName("IsTemplate")]
    public bool IsTemplate { get; set; }

    [JsonPropertyName("IsDefault")]
    public bool IsDefault { get; set; }
}
