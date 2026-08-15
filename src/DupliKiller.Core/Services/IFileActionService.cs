namespace DupliKiller.Core.Services;

public interface IFileActionService
{
    bool DeleteToRecycleBin(string filePath);
    bool DeletePermanently(string filePath);
    bool MoveFile(string sourcePath, string targetDirectory);
    Task<bool> CompressAndBackupAsync(string filePath, string backupDirectory);
}
