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

        if (DataContext is INotifyPropertyChanged propertyChanged)
        {
            propertyChanged.PropertyChanged += OnPropertyChanged;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Models.LifecycleView.FatalError))
        {
            Dispatcher.Invoke(() =>
            {
                if (DataContext is Models.LifecycleView viewModel &&
                    viewModel.FatalError is not null)
                {
                    ButtonPanel.Visibility = Visibility.Visible;
                    Browser.Visibility = Visibility.Hidden;
                }
            });
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ShowError_Click(object sender, RoutedEventArgs e)
    {
        if (ErrorDisplay.Visibility != Visibility.Visible)
        {
            ErrorDisplay.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorDisplay.Visibility = Visibility.Hidden;
        }
    }
}
