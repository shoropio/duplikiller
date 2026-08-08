using System.IO.Compression;
using System.Runtime.InteropServices;
using DuplicateFinder.Core.Logging;

namespace DuplicateFinder.Core.Services;

public class FileActionService : IFileActionService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)] public int wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    private const int FO_DELETE = 0x0003;
    private const short FOF_ALLOWUNDO = 0x0040;
    private const short FOF_NOCONFIRMATION = 0x0010;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    public bool DeleteToRecycleBin(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Logger.Warning($"DeleteToRecycleBin: file not found {filePath}");
            return false;
        }

        try
        {
            var doubleNullPath = filePath + '\0' + '\0';

            var fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = doubleNullPath,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION,
                hwnd = IntPtr.Zero
            };

            int result = SHFileOperation(ref fileOp);
            bool success = result == 0 && !fileOp.fAnyOperationsAborted;
            if (success)
                Logger.Info($"Moved to Recycle Bin: {filePath}");
            else
                Logger.Warning($"SHFileOperation failed for {filePath}: result={result}");
            return success;
        }
        catch (Exception ex)
        {
            Logger.Warning($"DeleteToRecycleBin failed, falling back to File.Delete: {ex.Message}");
            try
            {
                File.Delete(filePath);
                Logger.Info($"Fallback delete succeeded: {filePath}");
                return true;
            }
            catch (Exception ex2)
            {
                Logger.Error($"Fallback delete also failed: {filePath}: {ex2.Message}");
                return false;
            }
        }
    }

    public bool DeletePermanently(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                var attr = File.GetAttributes(filePath);
                if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                }

                File.Delete(filePath);
                Logger.Info($"Permanently deleted: {filePath}");
                return true;
            }
            Logger.Warning($"DeletePermanently: file not found {filePath}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"DeletePermanently failed: {filePath}: {ex.Message}");
            return false;
        }
    }

    public bool MoveFile(string sourcePath, string targetDirectory)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                Logger.Warning($"MoveFile: source not found {sourcePath}");
                return false;
            }
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
                Logger.Info($"Created target directory: {targetDirectory}");
            }

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(targetDirectory, fileName);

            int count = 1;
            while (File.Exists(destPath))
            {
                var nameOnly = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                destPath = Path.Combine(targetDirectory, $"{nameOnly}_{count}{ext}");
                count++;
            }

            File.Move(sourcePath, destPath);
            Logger.Info($"Moved: {sourcePath} -> {destPath}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"MoveFile failed: {sourcePath} -> {targetDirectory}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CompressAndBackupAsync(string filePath, string backupDirectory)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Logger.Warning($"CompressAndBackup: file not found {filePath}");
                return false;
            }
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
                Logger.Info($"Created backup directory: {backupDirectory}");
            }

            var fileName = Path.GetFileName(filePath);
            var zipName = Path.ChangeExtension(fileName, ".zip");
            var zipPath = Path.Combine(backupDirectory, zipName);

            await Task.Run(() =>
            {
                using var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                zipArchive.CreateEntryFromFile(filePath, fileName, CompressionLevel.Optimal);
            });

            bool exists = File.Exists(zipPath);
            if (exists)
                Logger.Info($"Backup created: {zipPath}");
            else
                Logger.Error($"Backup file not found after compression: {zipPath}");
            return exists;
        }
        catch (Exception ex)
        {
            Logger.Error($"CompressAndBackup failed: {filePath}: {ex.Message}");
            return false;
        }
    }
}
