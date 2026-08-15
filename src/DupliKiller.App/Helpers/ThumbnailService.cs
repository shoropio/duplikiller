using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DupliKiller.App.Helpers;

public static class ThumbnailService
{
    private const int DecodeWidth = 96;

    private static readonly ConcurrentDictionary<string, ImageSource> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".bmp", ".gif", ".ico", ".tif", ".tiff", ".wdp", ".webp"
    };

    public static bool IsImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        return ImageExtensions.Contains(System.IO.Path.GetExtension(path));
    }

    public static ImageSource? GetThumbnail(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = DecodeWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            Cache[path] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }
}
