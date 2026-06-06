using System.Windows;
using System.Windows.Controls;
using WinLayout.Models;
using WinLayout.Services;

namespace WinLayout.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private UserConfig _config;

    /// <summary>Fired when settings are saved. Subscriber should reload config-dependent services.</summary>
    public event Action? SettingsSaved;

    public SettingsWindow(ConfigService configService)
    {
        InitializeComponent();
        _configService = configService;
        _config = _configService.LoadConfig();

        LoadConfigToUI();
    }

    private void LoadConfigToUI()
    {
        ModifierKeyCombo.SelectedIndex = _config.ModifierKey switch
        {
            "None" => 0, "Shift" => 1, "Ctrl" => 2, "Alt" => 3,
            "Shift+Ctrl" => 4, "Shift+Alt" => 5, "Ctrl+Alt" => 6,
            _ => 0
        };

        StackingKeyCombo.SelectedIndex = _config.StackingKey switch
        {
            "Shift" => 0, "Ctrl" => 1, "Alt" => 2, _ => 1
        };

        DragThresholdSlider.Value = _config.DragThreshold;
        DragThresholdLabel.Text = $"{_config.DragThreshold}px";
        DragThresholdSlider.ValueChanged += (_, _) =>
        {
            _config.DragThreshold = (int)DragThresholdSlider.Value;
            DragThresholdLabel.Text = $"{_config.DragThreshold}px";
        };

        HighlightOpacitySlider.Value = (int)(_config.HighlightOpacity * 100);
        HighlightOpacityLabel.Text = $"{(int)HighlightOpacitySlider.Value}%";
        HighlightOpacitySlider.ValueChanged += (_, _) =>
        {
            _config.HighlightOpacity = HighlightOpacitySlider.Value / 100.0;
            HighlightOpacityLabel.Text = $"{(int)HighlightOpacitySlider.Value}%";
        };

        MinWidthBox.Text = _config.MinWindowSize.Width.ToString();
        MinWidthBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MinWidthBox.Text, out int v)) _config.MinWindowSize.Width = v;
        };
        MinHeightBox.Text = _config.MinWindowSize.Height.ToString();
        MinHeightBox.TextChanged += (_, _) =>
        {
            if (int.TryParse(MinHeightBox.Text, out int v)) _config.MinWindowSize.Height = v;
        };

        RunAtStartupCheck.IsChecked = _config.RunAtStartup;

        BlacklistListBox.Items.Clear();
        foreach (var proc in _config.BlacklistedProcesses)
            BlacklistListBox.Items.Add(proc);

        PerDesktopLayoutCheck.IsChecked = _config.PerDesktopLayout;
        AutoApplyOnSwitchCheck.IsChecked = _config.AutoApplyOnDesktopSwitch;

        ShowWindowButtonsCheck.IsChecked = _config.ShowWindowButtons;
        MaxZoneButtonSlider.Value = _config.MaxZoneButtonCount;
        MaxZoneButtonLabel.Text = _config.MaxZoneButtonCount.ToString();
        MaxZoneButtonSlider.ValueChanged += (_, _) =>
        {
            _config.MaxZoneButtonCount = (int)MaxZoneButtonSlider.Value;
            MaxZoneButtonLabel.Text = _config.MaxZoneButtonCount.ToString();
        };
    }

    private void CollectChanges()
    {
        _config.ModifierKey = ModifierKeyCombo.SelectedIndex switch
        {
            0 => "None", 1 => "Shift", 2 => "Ctrl", 3 => "Alt",
            4 => "Shift+Ctrl", 5 => "Shift+Alt", 6 => "Ctrl+Alt",
            _ => "None"
        };
        _config.StackingKey = StackingKeyCombo.SelectedIndex switch
        {
            0 => "Shift", 1 => "Ctrl", 2 => "Alt", _ => "Ctrl"
        };
        _config.RunAtStartup = RunAtStartupCheck.IsChecked == true;
        _config.PerDesktopLayout = PerDesktopLayoutCheck.IsChecked == true;
        _config.AutoApplyOnDesktopSwitch = AutoApplyOnSwitchCheck.IsChecked == true;
        _config.ShowWindowButtons = ShowWindowButtonsCheck.IsChecked == true;
    }

    private void OnSaveClose(object sender, RoutedEventArgs e)
    {
        CollectChanges();
        _configService.SaveConfig(_config);
        App.SetStartup(_config.RunAtStartup);
        SettingsSaved?.Invoke();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnAddBlacklist(object sender, RoutedEventArgs e)
    {
        var proc = BlacklistAddBox.Text.Trim();
        if (string.IsNullOrEmpty(proc) || _config.BlacklistedProcesses.Contains(proc))
            return;
        _config.BlacklistedProcesses.Add(proc);
        BlacklistListBox.Items.Add(proc);
    }

    private void OnRemoveBlacklist(object sender, RoutedEventArgs e)
    {
        if (BlacklistListBox.SelectedItem is string proc)
        {
            _config.BlacklistedProcesses.Remove(proc);
            BlacklistListBox.Items.Remove(proc);
        }
    }
}
