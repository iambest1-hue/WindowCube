using System.Windows;

namespace WinLayout.Views;

public partial class DonateWindow : Window
{
    public DonateWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
    }
}
