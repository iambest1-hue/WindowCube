using WinLayout.Models;

namespace WinLayout.Services;

public class LayoutService
{
    private readonly ConfigService _config;

    public LayoutService(ConfigService config)
    {
        _config = config;
    }

    public List<LayoutDefinition> GetAllLayouts()
    {
        var layouts = _config.LoadAllLayouts();

        // If no layouts exist, seed with default left-right split
        if (layouts.Count == 0)
        {
            var defaultLayout = new LayoutDefinition
            {
                Name = "默认左右二分",
                Zones = PresetTemplates.All[0].Zones
            };
            _config.SaveLayout(defaultLayout);
            layouts.Add(defaultLayout);

            // Set as active for current screen
            var userConfig = _config.LoadConfig();
            if (!userConfig.ScreenLayouts.ContainsKey("default"))
            {
                userConfig.ScreenLayouts["default"] = new ScreenLayoutConfig
                {
                    ActiveLayoutId = defaultLayout.LayoutId
                };
                _config.SaveConfig(userConfig);
            }
        }

        return layouts;
    }

    public LayoutDefinition? GetActiveLayout()
    {
        var config = _config.LoadConfig();
        if (config.ScreenLayouts.TryGetValue("default", out var screenCfg))
        {
            var layouts = _config.LoadAllLayouts();
            return layouts.FirstOrDefault(l => l.LayoutId == screenCfg.ActiveLayoutId);
        }
        return GetAllLayouts().FirstOrDefault();
    }

    public void Save(LayoutDefinition layout)
    {
        _config.SaveLayout(layout);

        // Update active layout if not set
        var config = _config.LoadConfig();
        if (!config.ScreenLayouts.ContainsKey("default"))
        {
            config.ScreenLayouts["default"] = new ScreenLayoutConfig
            {
                ActiveLayoutId = layout.LayoutId
            };
            _config.SaveConfig(config);
        }
    }

    public void SetActive(string layoutId)
    {
        var config = _config.LoadConfig();
        if (!config.ScreenLayouts.ContainsKey("default"))
            config.ScreenLayouts["default"] = new ScreenLayoutConfig();
        config.ScreenLayouts["default"].ActiveLayoutId = layoutId;
        _config.SaveConfig(config);
    }

    public void Delete(string layoutId)
    {
        _config.DeleteLayout(layoutId);
    }
}
