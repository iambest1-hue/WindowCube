using System.Windows;
using System.Windows.Controls;
using WinLayout.Models;
using WinLayout.Services;

namespace WinLayout.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private UserConfig _config;

    public SettingsWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;
        _config = _configService.LoadConfig();

        LoadConfigToUI();
        SetupBindings();
    }

    private void LoadConfigToUI()
    {
        // Modifier key
        var modIndex = _config.ModifierKey switch
        {
            "Shift" => 0, "Ctrl" => 1, "Alt" => 2,
            "Shift+Ctrl" => 3, "Shift+Alt" => 4, "Ctrl+Alt" => 5,
            _ => 0
        };
        ModifierKeyCombo.SelectedIndex = modIndex;

        // Stacking key
        StackingKeyCombo.SelectedIndex = _config.StackingKey switch
        {
            "Shift" => 0, "Ctrl" => 1, "Alt" => 2, _ => 1
        };

        // Drag threshold
        DragThresholdSlider.Value = _config.DragThreshold;
        DragThresholdLabel.Text = $"{_config.DragThreshold}px";

        // Min window size
        MinWidthBox.Text = _config.MinWindowSize.Width.ToString();
        MinHeightBox.Text = _config.MinWindowSize.Height.ToString();

        // Highlight opacity
        var opacityPct = (int)(_config.HighlightOpacity * 100);
        HighlightOpacitySlider.Value = opacityPct;
        HighlightOpacityLabel.Text = $"{opacityPct}%";

        // Startup
        RunAtStartupCheck.IsChecked = _config.RunAtStartup;

        // Blacklist
        BlacklistListBox.Items.Clear();
        foreach (var proc in _config.BlacklistedProcesses)
            BlacklistListBox.Items.Add(proc);

        // Virtual desktop
        PerDesktopLayoutCheck.IsChecked = _config.PerDesktopLayout;
        AutoApplyOnSwitchCheck.IsChecked = _config.AutoApplyOnDesktopSwitch;
    }

    private void SetupBindings()
    {
        ModifierKeyCombo.SelectionChanged += (_, _) => SaveConfig();
        StackingKeyCombo.SelectionChanged += (_, _) => SaveConfig();
        DragThresholdSlider.ValueChanged += (_, _) =>
        {
            _config.DragThreshold = (int)DragThresholdSlider.Value;
            DragThresholdLabel.Text = $"{_config.DragThreshold}px";
            SaveConfig();
        };
        HighlightOpacitySlider.ValueChanged += (_, _) =>
        {
            _config.HighlightOpacity = HighlightOpacitySlider.Value / 100.0;
            HighlightOpacityLabel.Text = $"{(int)HighlightOpacitySlider.Value}%";
            SaveConfig();
        };
        MinWidthBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MinWidthBox.Text, out int v))
            {
                _config.MinWindowSize.Width = v;
                SaveConfig();
            }
        };
        MinHeightBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MinHeightBox.Text, out int v))
            {
                _config.MinWindowSize.Height = v;
                SaveConfig();
            }
        };
        RunAtStartupCheck.Checked += (_, _) => { _config.RunAtStartup = true; SaveConfig(); App.SetStartup(true); };
        RunAtStartupCheck.Unchecked += (_, _) => { _config.RunAtStartup = false; SaveConfig(); App.SetStartup(false); };
        PerDesktopLayoutCheck.Checked += (_, _) => { _config.PerDesktopLayout = true; SaveConfig(); };
        PerDesktopLayoutCheck.Unchecked += (_, _) => { _config.PerDesktopLayout = false; SaveConfig(); };
        AutoApplyOnSwitchCheck.Checked += (_, _) => { _config.AutoApplyOnDesktopSwitch = true; SaveConfig(); };
        AutoApplyOnSwitchCheck.Unchecked += (_, _) => { _config.AutoApplyOnDesktopSwitch = false; SaveConfig(); };
    }

    private void SaveConfig()
    {
        _config.ModifierKey = ModifierKeyCombo.SelectedIndex switch
        {
            0 => "Shift", 1 => "Ctrl", 2 => "Alt",
            3 => "Shift+Ctrl", 4 => "Shift+Alt", 5 => "Ctrl+Alt",
            _ => "Shift"
        };
        _config.StackingKey = StackingKeyCombo.SelectedIndex switch
        {
            0 => "Shift", 1 => "Ctrl", 2 => "Alt", _ => "Ctrl"
        };

        _configService.SaveConfig(_config);
    }

    private void OnAddBlacklist(object sender, RoutedEventArgs e)
    {
        var proc = BlacklistAddBox.Text.Trim();
        if (string.IsNullOrEmpty(proc) || _config.BlacklistedProcesses.Contains(proc))
            return;

        _config.BlacklistedProcesses.Add(proc);
        BlacklistListBox.Items.Add(proc);
        SaveConfig();
    }

    private void OnRemoveBlacklist(object sender, RoutedEventArgs e)
    {
        if (BlacklistListBox.SelectedItem is string proc)
        {
            _config.BlacklistedProcesses.Remove(proc);
            BlacklistListBox.Items.Remove(proc);
            SaveConfig();
        }
    }
}
