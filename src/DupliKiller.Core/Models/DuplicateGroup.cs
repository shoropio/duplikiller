using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DupliKiller.Core.Models;

public class DuplicateGroup : INotifyPropertyChanged
{
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }

    public DuplicateGroup()
    {
        _files.CollectionChanged += OnFilesChanged;
    }

    private int _groupNumber;
    public int GroupNumber
    {
        get => _groupNumber;
        set { _groupNumber = value; OnPropertyChanged(); }
    }
    private ObservableCollection<FileItem> _files = new();
    public ObservableCollection<FileItem> Files
    {
        get => _files;
        set
        {
            if (_files != null) _files.CollectionChanged -= OnFilesChanged;
            _files = value;
            if (_files != null) _files.CollectionChanged += OnFilesChanged;
        }
    }

    private void OnFilesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(CopyCount));
        OnPropertyChanged(nameof(ReclaimableSpace));
        OnPropertyChanged(nameof(FriendlySize));
        OnPropertyChanged(nameof(FriendlyReclaimable));
        OnPropertyChanged(nameof(OriginalFile));
        OnPropertyChanged(nameof(CopyFiles));
    }

    public int FileCount => Files.Count;

    public int CopyCount => Files.Count - 1;

    public long ReclaimableSpace => CopyCount * FileSize;

    public FileItem? OriginalFile => Files.Count > 0 ? Files[0] : null;

    public IEnumerable<FileItem> CopyFiles => Files.Skip(1);

    public string FriendlySize
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double size = FileSize;
            int index = 0;
            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:F2} {suffixes[index]}";
        }
    }

    public string FriendlyReclaimable
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            double size = ReclaimableSpace;
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

    public void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(CopyCount));
        OnPropertyChanged(nameof(ReclaimableSpace));
        OnPropertyChanged(nameof(FriendlySize));
        OnPropertyChanged(nameof(FriendlyReclaimable));
        OnPropertyChanged(nameof(OriginalFile));
        OnPropertyChanged(nameof(CopyFiles));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
