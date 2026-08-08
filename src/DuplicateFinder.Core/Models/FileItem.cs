using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DuplicateFinder.Core.Models;

public class FileItem : INotifyPropertyChanged
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime LastWriteTime { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string? QuickHash { get; set; }
    public string? FullHash { get; set; }

    public bool IsLocked { get; set; }
    public bool IsSystem { get; set; }
    public bool IsHidden { get; set; }
    public string Owner { get; set; } = "Unknown";
    public string Permissions { get; set; } = "Read/Write";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string FriendlySize
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double size = Size;
            int index = 0;
            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:F2} {suffixes[index]}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
