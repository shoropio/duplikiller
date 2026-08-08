using System.Windows;

namespace DuplicateFinder.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
