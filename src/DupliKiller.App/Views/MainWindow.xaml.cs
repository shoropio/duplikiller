using System;
using System.IO;
using System.Windows;
using System.Windows.Data;
using DupliKiller.App.Views.ViewModels;

namespace DupliKiller.App.Views;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool inverse = parameter?.ToString() == "inverse";
        bool isNull = value == null;
        return (inverse ^ isNull) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotImplementedException();
}

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

    private void ScanPathsListBox_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Copy;
        else
            e.Effects = System.Windows.DragDropEffects.None;
    }

    private void ScanPathsListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var paths = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            foreach (var path in paths)
            {
                if (Directory.Exists(path) && DataContext is MainViewModel vm)
                {
                    if (!vm.ScanPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                        vm.ScanPaths.Add(path);
                }
            }
        }
    }
}