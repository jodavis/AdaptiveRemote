using System.ComponentModel;
using System.Windows;

namespace AdaptiveRemote;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(Models.LifecycleView viewModel)
    {
        DataContext = viewModel;

        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        Browser.WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0x00, 0x22, 0x22, 0x22);
    }
}
