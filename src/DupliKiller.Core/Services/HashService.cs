using System.Security.Cryptography;
using System.Text;
using DupliKiller.Core.Logging;

namespace DupliKiller.Core.Services;

public class HashService : IHashService
{
    private const int BufferSize = 256 * 1024;
    private const int QuickHashSize = 4096;

    public string ComputeQuickHash(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        long length = stream.Length;
        if (length == 0) return "empty_0";

        // For small files, hash the whole content. For larger files, sample from start/middle/end.
        if (length <= QuickHashSize)
        {
            var whole = new byte[length];
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(whole, totalRead, (int)(length - totalRead));
                if (read == 0) break;
                totalRead += read;
            }

            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(whole, 0, totalRead);
            var sb = new StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
            sb.Append('_').Append(length);
            return sb.ToString();
        }

        int piece = QuickHashSize / 3;
        if (piece < 16) piece = QuickHashSize; // fallback

        var sample = new byte[piece * 3];
        int offset = 0;

        // Read start
        stream.Seek(0, SeekOrigin.Begin);
        int readStart = stream.Read(sample, offset, piece);
        offset += readStart;

        // Read middle
        long midPos = Math.Max(0, (length / 2) - (piece / 2));
        stream.Seek(midPos, SeekOrigin.Begin);
        int readMid = stream.Read(sample, offset, piece);
        offset += readMid;

        // Read end
        long endPos = Math.Max(0, length - piece);
        stream.Seek(endPos, SeekOrigin.Begin);
        int readEnd = stream.Read(sample, offset, piece);
        offset += readEnd;

        using var md5b = MD5.Create();
        var hashBytes2 = md5b.ComputeHash(sample, 0, offset);
        var sb2 = new StringBuilder();
        foreach (var b in hashBytes2) sb2.Append(b.ToString("x2"));
        sb2.Append('_').Append(length);
        return sb2.ToString();
    }

    public string ComputeFullHash(string filePath, string algorithm)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
        using HashAlgorithm hasher = algorithm.ToUpper() switch
        {
            "SHA256" => SHA256.Create(),
            "SHA1" => SHA1.Create(),
            _ => SHA256.Create()
        };

        var hashBytes = hasher.ComputeHash(stream);
        if (hashBytes == null) throw new InvalidOperationException("Error computing hash.");

        var sb = new StringBuilder();
        foreach (var b in hashBytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public bool CanReadFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, BufferSize);
            var buffer = new byte[BufferSize];
            int total = 0;
            while (total < BufferSize)
            {
                int read = stream.Read(buffer, total, BufferSize - total);
                if (read == 0) break;
                total += read;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ConfirmBinaryEquality(string filePath1, string filePath2)
    {
        try
        {
            var fileInfo1 = new FileInfo(filePath1);
            var fileInfo2 = new FileInfo(filePath2);

            if (fileInfo1.Length != fileInfo2.Length) return false;

            using var fs1 = new FileStream(filePath1, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);
            using var fs2 = new FileStream(filePath2, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize);

            var buffer1 = new byte[BufferSize];
            var buffer2 = new byte[BufferSize];

            while (true)
            {
                int read1 = fs1.Read(buffer1, 0, BufferSize);
                int read2 = fs2.Read(buffer2, 0, BufferSize);

                if (read1 != read2) return false;
                if (read1 == 0) return true;

                if (!buffer1.AsSpan(0, read1).SequenceEqual(buffer2.AsSpan(0, read2)))
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Binary equality compare failed: {filePath1} vs {filePath2}: {ex.Message}");
            return false;
        }
    }
}
