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
}
