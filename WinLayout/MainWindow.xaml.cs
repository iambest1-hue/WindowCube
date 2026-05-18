using System.Windows;
using WinLayout.Services;

namespace WinLayout;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();

    public MainWindow()
    {
        InitializeComponent();
        var config = _configService.LoadConfig();
        Title = $"WinLayout — {config.ModifierKey}+拖拽吸附就绪";
    }
}
