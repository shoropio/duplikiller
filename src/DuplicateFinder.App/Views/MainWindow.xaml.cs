using System.Windows;
using DuplicateFinder.App.Views.ViewModels;

namespace DuplicateFinder.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void OpenMenu(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.ContextMenu != null)
        {
            fe.ContextMenu.PlacementTarget = fe;
            fe.ContextMenu.IsOpen = true;
        }
    }
}
