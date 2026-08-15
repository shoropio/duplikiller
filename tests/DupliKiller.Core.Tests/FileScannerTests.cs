using DupliKiller.Core.Models;
using DupliKiller.Core.Services;
using DupliKiller.Core.Utils;

namespace DupliKiller.Core.Tests;

public class FileScannerTests
{
    [Fact]
    public async Task RunScanAsync_WithDuplicateFiles_ReturnsDuplicateGroups()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "a.txt");
            var file2 = Path.Combine(tempDir, "b.txt");
            await File.WriteAllTextAsync(file1, "same content");
            await File.WriteAllTextAsync(file2, "same content");

            var scanner = new FileScanner(new HashService(), new SystemProtector());
            var result = await scanner.RunScanAsync(
                new List<string> { tempDir },
                new List<string>(),
                new List<string>(),
                true,
                true,
                0,
                0,
                null,
                null,
                "SHA256",
                CancellationToken.None,
                new Progress<ScanStatsUpdate>());

            Assert.Single(result.DuplicateGroups);
            Assert.Equal(2, result.DuplicateGroups[0].Files.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScanAsync_DeepMode_WithDuplicateFiles_ReturnsDuplicateGroups()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var file1 = Path.Combine(tempDir, "a.txt");
            var file2 = Path.Combine(tempDir, "b.txt");
            var file3 = Path.Combine(tempDir, "c.txt");
            await File.WriteAllTextAsync(file1, "same content");
            await File.WriteAllTextAsync(file2, "same content");
            await File.WriteAllTextAsync(file3, "different content");

            var scanner = new FileScanner(new HashService(), new SystemProtector());
            var result = await scanner.RunScanAsync(
                new List<string> { tempDir },
                new List<string>(),
                new List<string>(),
                true,
                true,
                0,
                0,
                null,
                null,
                "SHA256",
                CancellationToken.None,
                new Progress<ScanStatsUpdate>(),
                ScanMode.Deep);

            Assert.Single(result.DuplicateGroups);
            Assert.Equal(2, result.DuplicateGroups[0].Files.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScanAsync_InvalidTargetPath_IsSkippedWithoutThrowing()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), "dupfinder-tests", "does-not-exist", Guid.NewGuid().ToString("N"));

        var scanner = new FileScanner(new HashService(), new SystemProtector());
        var result = await scanner.RunScanAsync(
            new List<string> { invalidPath },
            new List<string>(),
            new List<string>(),
            true,
            true,
            0,
            0,
            null,
            null,
            "SHA256",
            CancellationToken.None,
            new Progress<ScanStatsUpdate>());

        Assert.Empty(result.DuplicateGroups);
        Assert.Empty(result.ZeroByteFiles);
        Assert.Empty(result.CorruptedFiles);
    }

    [Fact]
    public async Task RunScanAsync_ZeroByteFiles_AreReportedAndExcludedFromDuplicates()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var empty1 = Path.Combine(tempDir, "empty1.txt");
            var empty2 = Path.Combine(tempDir, "empty2.log");
            var normal = Path.Combine(tempDir, "normal.txt");
            await File.WriteAllTextAsync(empty1, string.Empty);
            await File.WriteAllTextAsync(empty2, string.Empty);
            await File.WriteAllTextAsync(normal, "some content");

            var scanner = new FileScanner(new HashService(), new SystemProtector());
            var result = await scanner.RunScanAsync(
                new List<string> { tempDir },
                new List<string>(),
                new List<string>(),
                true,
                true,
                0,
                0,
                null,
                null,
                "SHA256",
                CancellationToken.None,
                new Progress<ScanStatsUpdate>());

            Assert.Equal(2, result.ZeroByteFiles.Count);
            Assert.All(result.ZeroByteFiles, f =>
            {
                Assert.Equal(0, f.Size);
                Assert.Equal(FileIssueType.ZeroByte, f.FileIssue);
                Assert.NotNull(f.IssueReason);
            });
            Assert.DoesNotContain(result.DuplicateGroups, g => g.Files.Any(f => f.Size == 0));
            Assert.Empty(result.CorruptedFiles);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScanAsync_CorruptedFiles_AreReportedAsCorrupted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var locked = Path.Combine(tempDir, "locked.txt");
            var normal = Path.Combine(tempDir, "normal.txt");
            await File.WriteAllTextAsync(locked, "content that cannot be read");
            await File.WriteAllTextAsync(normal, "content that cannot be read");

            // Hold an exclusive lock so the scanner cannot open the file for reading.
            using var exclusive = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var scanner = new FileScanner(new HashService(), new SystemProtector());
            var result = await scanner.RunScanAsync(
                new List<string> { tempDir },
                new List<string>(),
                new List<string>(),
                true,
                true,
                0,
                0,
                null,
                null,
                "SHA256",
                CancellationToken.None,
                new Progress<ScanStatsUpdate>());

            var corrupted = Assert.Single(result.CorruptedFiles);
            Assert.Equal(FileIssueType.Corrupted, corrupted.FileIssue);
            Assert.Equal(locked, corrupted.Path);
            Assert.NotNull(corrupted.IssueReason);
            Assert.DoesNotContain(result.DuplicateGroups, g => g.Files.Any(f => f.Path == locked));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunScanAsync_UniqueSizeCorruptedFile_IsReportedAsCorrupted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var locked = Path.Combine(tempDir, "solo-locked.txt");
            await File.WriteAllTextAsync(locked, "unique size, cannot be read");

            // Exclusive lock so the file has a unique size and cannot be read.
            using var exclusive = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var scanner = new FileScanner(new HashService(), new SystemProtector());
            var result = await scanner.RunScanAsync(
                new List<string> { tempDir },
                new List<string>(),
                new List<string>(),
                true,
                true,
                0,
                0,
                null,
                null,
                "SHA256",
                CancellationToken.None,
                new Progress<ScanStatsUpdate>());

            var corrupted = Assert.Single(result.CorruptedFiles);
            Assert.Equal(FileIssueType.Corrupted, corrupted.FileIssue);
            Assert.Equal(locked, corrupted.Path);
            Assert.Empty(result.DuplicateGroups);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

public class ExportServiceTests
{
    [Fact]
    public void ExportToCsv_EscapesCommasQuotesAndNames()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "dupfinder-tests", Guid.NewGuid().ToString("N") + ".csv");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);

        try
        {
            var group = new DuplicateGroup
            {
                Hash = "abc",
                FileSize = 10
            };
            group.Files.Add(new FileItem
            {
                Name = "archivo, \"especial\".txt",
                Path = @"C:\carpeta con espacio\archivo, ""especial"".txt",
                Size = 10
            });
            group.Files.Add(new FileItem
            {
                Name = "normal.txt",
                Path = @"C:\normal.txt",
                Size = 10
            });

            var exportService = new ExportService();
            exportService.ExportToCsv(tempFile, new List<DuplicateGroup> { group });

            var lines = File.ReadAllLines(tempFile);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("\"", lines[1]);
            Assert.Contains("\"\"", lines[1]);
            Assert.Contains(",10,", lines[1]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
