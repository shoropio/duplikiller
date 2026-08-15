using DupliKiller.Core.Models;

namespace DupliKiller.Core.Services;

public interface IFileScanner
{
    event Action<int, string>? FileDiscovered;
    event Action<FileItem>? FileHashed;
    event Action<string>? StatusChanged;
    event Action<int, int>? ProgressUpdated;
    event Action<ScanPhase, string>? PhaseChanged;

    Task<ScanResult> RunScanAsync(
        List<string> targetPaths,
        List<string> excludePaths,
        List<string> excludeExtensions,
        bool excludeHidden,
        bool excludeSystem,
        long minSize,
        long maxSize,
        DateTime? minDate,
        DateTime? maxDate,
        string algorithm,
        CancellationToken cancellationToken,
        IProgress<ScanStatsUpdate> progress,
        ScanMode scanMode = ScanMode.Standard);
}

public enum ScanMode
{
    Standard,
    Deep
}

public enum ScanPhase
{
    Discovering,
    Grouping,
    QuickHashing,
    FullHashing,
    Comparing,
    Completed,
    Cancelled,
    Failed
}

public record ScanStatsUpdate(
    int FilesScanned,
    int DuplicatesCount,
    int GroupsCount,
    long ReclaimableSpace,
    int HashesCalculated,
    double ElapsedSeconds,
    string PhaseName,
    int ZeroByteCount = 0,
    int CorruptedCount = 0);
