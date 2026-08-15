namespace DupliKiller.Core.Models;

public class ScanResult
{
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
    public List<FileItem> ZeroByteFiles { get; set; } = new();
    public List<FileItem> CorruptedFiles { get; set; } = new();

    public bool HasProblemFiles => ZeroByteFiles.Count > 0 || CorruptedFiles.Count > 0;
    public int ProblemFilesCount => ZeroByteFiles.Count + CorruptedFiles.Count;
}
