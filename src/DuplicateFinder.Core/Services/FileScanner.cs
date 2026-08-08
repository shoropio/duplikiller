using System.Collections.Concurrent;
using DuplicateFinder.Core.Logging;
using DuplicateFinder.Core.Models;
using DuplicateFinder.Core.Utils;

namespace DuplicateFinder.Core.Services;

public class FileScanner : IFileScanner
{
    private readonly IHashService _hashService;
    private readonly SystemProtector _systemProtector;

    public event Action<int, string>? FileDiscovered;
    public event Action<FileItem>? FileHashed;
    public event Action<string>? StatusChanged;
    public event Action<int, int>? ProgressUpdated;
    public event Action<ScanPhase, string>? PhaseChanged;

    public FileScanner(IHashService hashService, SystemProtector systemProtector)
    {
        _hashService = hashService;
        _systemProtector = systemProtector;
    }

    public async Task<List<DuplicateGroup>> RunScanAsync(
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
        ScanMode scanMode = ScanMode.Standard)
    {
        var startedAt = DateTime.UtcNow;
        Logger.Info($"Scan started. Paths: {string.Join(", ", targetPaths)}");

        var invalidTargetPaths = targetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
            .ToList();
        foreach (var invalidPath in invalidTargetPaths)
        {
            Logger.Warning($"Target path does not exist or is inaccessible, skipped: {invalidPath}");
        }

        var validTargetPaths = targetPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .ToList();

        StatusChanged?.Invoke("Indexando archivos...");
        PhaseChanged?.Invoke(ScanPhase.Discovering, "Indexando archivos...");

        var normalizedExcludeExtensions = new HashSet<string>(
            excludeExtensions
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Select(ext => ext.StartsWith('.') ? ext : $".{ext}"),
            StringComparer.OrdinalIgnoreCase);

        var normalizedExcludePaths = excludePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToList();

        var fileEntries = new ConcurrentBag<(string Path, long Length, DateTime LastWrite)>();
        int discoveryCount = 0;

        var discoveryTasks = validTargetPaths
            .Select(path => Task.Run(() =>
            {
                try
                {
                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        AttributesToSkip = (excludeHidden ? FileAttributes.Hidden : 0) |
                                           (excludeSystem ? FileAttributes.System : 0)
                    };

                    var dirInfo = new DirectoryInfo(path);
                    foreach (var file in dirInfo.EnumerateFiles("*", options))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (normalizedExcludeExtensions.Contains(file.Extension))
                            continue;

                        if (minSize > 0 && file.Length < minSize) continue;
                        if (maxSize > 0 && file.Length > maxSize) continue;

                        if (minDate.HasValue && file.LastWriteTime < minDate.Value) continue;
                        if (maxDate.HasValue && file.LastWriteTime > maxDate.Value) continue;

                        if (normalizedExcludePaths.Any(p => file.FullName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        if (excludeSystem && _systemProtector.IsSystemDirectory(file.FullName)) continue;

                        fileEntries.Add((file.FullName, file.Length, file.LastWriteTime));
                        int currentCount = Interlocked.Increment(ref discoveryCount);

                        if (currentCount % 500 == 0)
                        {
                            FileDiscovered?.Invoke(currentCount, file.FullName);
                        }
                    }
                }
                catch (UnauthorizedAccessException ex) { Logger.Debug($"Access denied: {ex.Message}"); }
            }, cancellationToken));

        await Task.WhenAll(discoveryTasks);

        FileDiscovered?.Invoke(discoveryCount, "Indexing complete.");
        Logger.Info($"Discovered {discoveryCount} valid files. Size grouping...");
        StatusChanged?.Invoke($"Se encontraron {discoveryCount} archivos válidos. Agrupando por tamaño...");
        PhaseChanged?.Invoke(ScanPhase.Grouping, "Agrupando por tamaño...");

        var sizeGroupDict = new Dictionary<long, List<string>>();
        foreach (var entry in fileEntries)
        {
            if (!sizeGroupDict.TryGetValue(entry.Length, out var list))
            {
                list = new List<string>();
                sizeGroupDict[entry.Length] = list;
            }
            list.Add(entry.Path);
        }

        var candidatePaths = new List<string>();
        int sizeGroupsWithDupes = 0;
        foreach (var kvp in sizeGroupDict)
        {
            if (kvp.Value.Count > 1)
            {
                candidatePaths.AddRange(kvp.Value);
                sizeGroupsWithDupes++;
            }
        }

        int totalCandidates = candidatePaths.Count;
        Logger.Info($"{sizeGroupsWithDupes} size groups with duplicates. {totalCandidates} candidates for hashing.");
        int processedCount = 0;
        int hashesCalculated = 0;

        // Deep mode hashes the full file content directly, skipping the sampled quick hash.
        bool isDeep = scanMode == ScanMode.Deep;
        StatusChanged?.Invoke(isDeep
            ? $"Calculando hash completo ({totalCandidates} archivos)..."
            : $"Calculando hash rápido ({totalCandidates} archivos)...");
        PhaseChanged?.Invoke(isDeep ? ScanPhase.FullHashing : ScanPhase.QuickHashing,
            isDeep ? "Calculando hash completo..." : "Calculando hash rápido...");

        var fileItems = new ConcurrentDictionary<string, FileItem>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Task.Run(() =>
        {
            Parallel.ForEach(candidatePaths, parallelOptions, path =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var info = new FileInfo(path);
                    var primaryHash = isDeep
                        ? _hashService.ComputeFullHash(path, algorithm)
                        : _hashService.ComputeQuickHash(path);
                    Interlocked.Increment(ref hashesCalculated);

                    var fileItem = new FileItem
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        Size = info.Length,
                        CreationTime = info.CreationTime,
                        LastWriteTime = info.LastWriteTime,
                        Extension = info.Extension,
                        QuickHash = primaryHash,
                        FullHash = isDeep ? primaryHash : null,
                        IsSystem = _systemProtector.IsSystemDirectory(info.FullName),
                        IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                        IsLocked = IsFileLocked(info.FullName),
                        Permissions = info.Attributes.HasFlag(FileAttributes.ReadOnly)
                            ? "Solo lectura"
                            : "Lectura/Escritura"
                    };

                    fileItems.TryAdd(path, fileItem);

                    int current = Interlocked.Increment(ref processedCount);
                    if (current % 100 == 0 || current == totalCandidates)
                    {
                        ProgressUpdated?.Invoke(current, totalCandidates);
                        FileHashed?.Invoke(fileItem);
                    }
                }
                catch (Exception ex) { Logger.Debug($"Primary hash failed for {path}: {ex.Message}"); }
            });
        }, cancellationToken);

        ProgressUpdated?.Invoke(totalCandidates, totalCandidates);

        if (!isDeep)
        {
            StatusChanged?.Invoke("Verificando colisiones con hash completo...");

            var quickHashGroups = fileItems.Values
                .Where(fi => fi.QuickHash != null)
                .GroupBy(fi => fi.QuickHash)
                .Where(g => g.Count() > 1)
                .ToList();

            var collisionFiles = quickHashGroups.SelectMany(g => g).ToList();
            processedCount = 0;
            int collisionTotal = collisionFiles.Count;

            if (collisionTotal > 0)
            {
                StatusChanged?.Invoke($"Hash completo ({collisionTotal} archivos)...");
                PhaseChanged?.Invoke(ScanPhase.FullHashing, "Calculando hash completo...");

                await Task.Run(() =>
                {
                    Parallel.ForEach(collisionFiles, parallelOptions, item =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var fullHash = _hashService.ComputeFullHash(item.Path, algorithm);
                            Interlocked.Increment(ref hashesCalculated);
                            item.FullHash = fullHash;

                            int current = Interlocked.Increment(ref processedCount);
                            if (current % 100 == 0 || current == collisionTotal)
                            {
                                ProgressUpdated?.Invoke(current, collisionTotal);
                            }
                        }
                        catch (Exception ex) { Logger.Debug($"FullHash failed for {item.Path}: {ex.Message}"); }
                    });
                }, cancellationToken);

                ProgressUpdated?.Invoke(collisionTotal, collisionTotal);
            }
        }

        Logger.Info($"Hashing complete. {fileItems.Count} files hashed. Comparing binaries...");
        StatusChanged?.Invoke("Comparación binaria final...");
        PhaseChanged?.Invoke(ScanPhase.Comparing, "Comparando archivos...");

        var potentialGroups = fileItems.Values
            .Where(fi => fi.FullHash != null)
            .GroupBy(fi => fi.FullHash!)
            .Where(g => g.Count() > 1)
            .ToList();

        var finalGroups = new List<DuplicateGroup>();

        foreach (var potentialGroup in potentialGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var items = potentialGroup.ToList();

            while (items.Count > 0)
            {
                var pivot = items[0];
                items.RemoveAt(0);

                var matchedGroup = new DuplicateGroup
                {
                    Hash = pivot.FullHash!,
                    FileSize = pivot.Size
                };
                matchedGroup.Files.Add(pivot);

                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (_hashService.ConfirmBinaryEquality(pivot.Path, items[i].Path))
                    {
                        matchedGroup.Files.Add(items[i]);
                        items.RemoveAt(i);
                    }
                }

                if (matchedGroup.Files.Count > 1)
                {
                    finalGroups.Add(matchedGroup);
                }
            }
        }

        int finalDupCount = finalGroups.Sum(g => g.Files.Count - 1);
        long totalSpace = finalGroups.Sum(g => g.ReclaimableSpace);

        var elapsed = DateTime.UtcNow - startedAt;
        progress.Report(new ScanStatsUpdate(
            FilesScanned: discoveryCount,
            DuplicatesCount: finalDupCount,
            GroupsCount: finalGroups.Count,
            ReclaimableSpace: totalSpace,
            HashesCalculated: hashesCalculated,
            ElapsedSeconds: elapsed.TotalSeconds,
            PhaseName: "Completado"
        ));

        StatusChanged?.Invoke("Escaneo completado exitosamente.");
        PhaseChanged?.Invoke(ScanPhase.Completed, "Escaneo completado.");
        Logger.Info($"Scan complete. {finalGroups.Count} groups, {finalDupCount} dupes, {totalSpace} bytes.");
        return finalGroups;
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
