using System.Text;
using DuplicateFinder.Core.Logging;
using DuplicateFinder.Core.Models;

namespace DuplicateFinder.Core.Services;

public class ExportService : IExportService
{
    public void ExportToCsv(string path, List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HashGrupo,NombreArchivo,RutaCompleta,TamañoBytes,UltimaModificacion");

        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                sb.AppendLine($"{CsvEscape(group.Hash)},{CsvEscape(file.Name)},{CsvEscape(file.Path)},{file.Size},{file.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
            }
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Logger.Info($"CSV exported: {path} ({groups.Sum(g => g.Files.Count)} files)");
    }

    public void ExportToJson(string path, List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            sb.AppendLine("  {");
            sb.AppendLine($"    \"hash\": \"{EscapeJson(g.Hash)}\",");
            sb.AppendLine($"    \"fileSize\": {g.FileSize},");
            sb.AppendLine("    \"files\": [");

            for (int j = 0; j < g.Files.Count; j++)
            {
                var f = g.Files[j];
                sb.AppendLine("      {");
                sb.AppendLine($"        \"name\": \"{EscapeJson(f.Name)}\",");
                sb.AppendLine($"        \"path\": \"{EscapeJson(f.Path)}\",");
                sb.AppendLine($"        \"size\": {f.Size},");
                sb.AppendLine($"        \"modified\": \"{f.LastWriteTime:s}\"");
                sb.Append("      }");
                if (j < g.Files.Count - 1) sb.AppendLine(",");
                else sb.AppendLine();
            }

            sb.AppendLine("    ]");
            sb.Append("  }");
            if (i < groups.Count - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.AppendLine("]");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Logger.Info($"JSON exported: {path} ({groups.Sum(g => g.Files.Count)} files)");
    }

    public void ExportToXml(string path, List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<DuplicateGroups>");

        foreach (var group in groups)
        {
            sb.AppendLine($"  <Group Hash=\"{EscapeXml(group.Hash)}\" Size=\"{group.FileSize}\">");
            foreach (var file in group.Files)
            {
                sb.AppendLine("    <File>");
                sb.AppendLine($"      <Name>{EscapeXml(file.Name)}</Name>");
                sb.AppendLine($"      <Path>{EscapeXml(file.Path)}</Path>");
                sb.AppendLine($"      <Size>{file.Size}</Size>");
                sb.AppendLine($"      <LastWriteTime>{file.LastWriteTime:s}</LastWriteTime>");
                sb.AppendLine("    </File>");
            }
            sb.AppendLine("  </Group>");
        }
        sb.AppendLine("</DuplicateGroups>");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Logger.Info($"XML exported: {path} ({groups.Sum(g => g.Files.Count)} files)");
    }

    public void ExportToTxt(string path, List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===============================================================================");
        sb.AppendLine("              INFORME DETALLADO DE ARCHIVOS DUPLICADOS");
        sb.AppendLine($"              Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine("===============================================================================");
        sb.AppendLine();

        int groupIndex = 1;
        foreach (var group in groups)
        {
            sb.AppendLine($"Grupo {groupIndex++} | Hash: {group.Hash} | Tamaño: {group.FriendlySize} | Copias: {group.CopyCount}");
            sb.AppendLine("-------------------------------------------------------------------------------");
            foreach (var file in group.Files)
            {
                sb.AppendLine($" - {file.Path} ({file.FriendlySize})");
            }
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Logger.Info($"TXT exported: {path} ({groups.Sum(g => g.Files.Count)} files)");
    }

    private static string CsvEscape(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";

    private static string EscapeJson(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
