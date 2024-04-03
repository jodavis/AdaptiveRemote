using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace AdaptiveRemote;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        IServiceScope scope = serviceProvider.CreateScope();

        Resources.Add("services", scope.ServiceProvider);
    }
}
