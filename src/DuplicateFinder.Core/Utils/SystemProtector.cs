namespace DuplicateFinder.Core.Utils;

public class SystemProtector
{
    private static readonly string[] CoreSystemFolders = new[]
    {
        @"C:\Windows",
        @"C:\Program Files\WindowsApps",
        @"C:\ProgramData\Microsoft",
        @"C:\Users\All Users",
        @"System Volume Information"
    };

    private static readonly string[] CriticalExtensions = new[]
    {
        ".sys", ".dll", ".efi", ".msi", ".lnk", ".drv"
    };

    public bool IsSystemDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (CoreSystemFolders.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string tempPath = Path.Combine(localAppData, "Temp");

        if (path.StartsWith(appData, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
        {
            if (path.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        return false;
    }

    public bool IsCriticalFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (IsSystemDirectory(path)) return true;

        var ext = Path.GetExtension(path);
        if (CriticalExtensions.Any(ce => ce.Equals(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public bool IsSymbolicLink(string path)
    {
        try
        {
            var pathInfo = new FileInfo(path);
            return pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }
}
