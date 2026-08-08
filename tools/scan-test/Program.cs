using System;
using System.Threading;
using DuplicateFinder.Core.Services;
using DuplicateFinder.Core.Utils;

var target = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
Console.WriteLine($"ScanTest: starting (target: {target})");

var hashService = new HashService();
var protector = new SystemProtector();
var scanner = new FileScanner(hashService, protector);

scanner.StatusChanged += s => Console.WriteLine($"Status: {s}");
scanner.FileDiscovered += (count, path) => Console.WriteLine($"Discovered: {count} -> {path}");
scanner.FileHashed += item => Console.WriteLine($"Hashed: {item.Path}");
scanner.ProgressUpdated += (c, t) => Console.WriteLine($"Progress: {c}/{t}");

var cts = new CancellationTokenSource();

try
{
    var groups = scanner.RunScanAsync(
        new System.Collections.Generic.List<string> { target },
        new System.Collections.Generic.List<string>(),
        new System.Collections.Generic.List<string> { ".sys", ".dll" },
        true,
        true,
        0,
        0,
        null,
        null,
        "SHA256",
        cts.Token,
        new System.Progress<DuplicateFinder.Core.Services.ScanStatsUpdate>(s =>
        {
            Console.WriteLine($"Stats: files={s.FilesScanned}, groups={s.GroupsCount}, dupes={s.DuplicatesCount}");
        })).GetAwaiter().GetResult();

    Console.WriteLine($"Scan finished: {groups.Count} groups");
}
catch (Exception ex)
{
    Console.WriteLine($"Scan failed: {ex}");
}

Console.WriteLine("ScanTest: done");
