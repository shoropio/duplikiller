using System.Windows.Media;
using DupliKiller.Core.Models;

namespace DupliKiller.App.Views.ViewModels;

public class FileItemViewModel : FileItem
{
    private ImageSource? _thumbnail;

    public FileItemViewModel(FileItem source)
    {
        Name = source.Name;
        Path = source.Path;
        Size = source.Size;
        CreationTime = source.CreationTime;
        LastWriteTime = source.LastWriteTime;
        Extension = source.Extension;
        QuickHash = source.QuickHash;
        FullHash = source.FullHash;
        IsLocked = source.IsLocked;
        IsSystem = source.IsSystem;
        IsHidden = source.IsHidden;
        Owner = source.Owner;
        Permissions = source.Permissions;
        FileIssue = source.FileIssue;
        IssueReason = source.IssueReason;
        IsSelected = source.IsSelected;
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            OnPropertyChanged(nameof(Thumbnail));
        }
    }
}
