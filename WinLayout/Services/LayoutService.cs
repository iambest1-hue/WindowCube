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

        // Seed preset templates as default layouts if missing
        foreach (var preset in PresetTemplates.All)
        {
            var presetName = preset.Name + " (默认)";
            if (!layouts.Any(l => l.Name == presetName))
            {
                var defaultLayout = new LayoutDefinition
                {
                    Name = presetName,
                    Zones = preset.Zones.Select(z => new ZoneDefinition
                    {
                        Index = z.Index, Left = z.Left, Top = z.Top,
                        Width = z.Width, Height = z.Height, Padding = z.Padding
                    }).ToList(),
                    IsDefault = true,
                    IsFavorite = true
                };
                _config.SaveLayout(defaultLayout);
                layouts.Add(defaultLayout);
            }
        }

        // If no layouts are favorited (first run or upgrade), mark all as favorites
        if (layouts.Count > 0 && !layouts.Any(l => l.IsFavorite))
        {
            foreach (var l in layouts)
            {
                l.IsFavorite = true;
                _config.SaveLayout(l);
            }
        }

        // Set first layout as active if nothing is set
        if (layouts.Count > 0)
        {
            var userConfig = _config.LoadConfig();
            if (!userConfig.ScreenLayouts.ContainsKey("default") ||
                string.IsNullOrEmpty(userConfig.ScreenLayouts["default"].ActiveLayoutId))
            {
                if (!userConfig.ScreenLayouts.ContainsKey("default"))
                    userConfig.ScreenLayouts["default"] = new ScreenLayoutConfig();
                userConfig.ScreenLayouts["default"].ActiveLayoutId = layouts[0].LayoutId;
                _config.SaveConfig(userConfig);
            }
        }

        return layouts;
    }

    public LayoutDefinition? GetActiveLayout()
    {
        var config = _config.LoadConfig();
        if (config.ScreenLayouts.TryGetValue("default", out var screenCfg) &&
            !string.IsNullOrEmpty(screenCfg.ActiveLayoutId))
        {
            var layouts = _config.LoadAllLayouts();
            var active = layouts.FirstOrDefault(l => l.LayoutId == screenCfg.ActiveLayoutId);
            if (active != null)
                return active;

            // Active layout was deleted — fall back to first favorite
            if (layouts.Count > 0)
            {
                var next = layouts.FirstOrDefault(l => l.IsFavorite) ?? layouts[0];
                screenCfg.ActiveLayoutId = next.LayoutId;
                _config.SaveConfig(config);
                return next;
            }
        }
        return GetAllLayouts().FirstOrDefault();
    }

    public void Save(LayoutDefinition layout)
    {
        _config.SaveLayout(layout);

        // Don't overwrite IsDefault flag when saving
        var config = _config.LoadConfig();
        if (!config.ScreenLayouts.ContainsKey("default"))
            config.ScreenLayouts["default"] = new ScreenLayoutConfig();
        config.ScreenLayouts["default"].ActiveLayoutId = layout.LayoutId;
        _config.SaveConfig(config);
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

        // If the active layout was deleted, fall back to the first favorite
        var config = _config.LoadConfig();
        if (config.ScreenLayouts.TryGetValue("default", out var screenCfg) &&
            screenCfg.ActiveLayoutId == layoutId)
        {
            var layouts = _config.LoadAllLayouts();
            var next = layouts.FirstOrDefault(l => l.IsFavorite) ?? layouts.FirstOrDefault();
            screenCfg.ActiveLayoutId = next?.LayoutId ?? string.Empty;
            _config.SaveConfig(config);
        }
    }
}
