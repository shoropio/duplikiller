using DuplicateFinder.Core.Models;
using DuplicateFinder.Core.Services;
using DuplicateFinder.Core.Utils;

namespace DuplicateFinder.Core.Tests;

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
            var groups = await scanner.RunScanAsync(
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

            Assert.Single(groups);
            Assert.Equal(2, groups[0].Files.Count);
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
            var groups = await scanner.RunScanAsync(
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

            Assert.Single(groups);
            Assert.Equal(2, groups[0].Files.Count);
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
        var groups = await scanner.RunScanAsync(
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

        Assert.Empty(groups);
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
