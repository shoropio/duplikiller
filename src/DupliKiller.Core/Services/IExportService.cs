using DupliKiller.Core.Models;

namespace DupliKiller.Core.Services;

public interface IExportService
{
    void ExportToCsv(string path, List<DuplicateGroup> groups);
    void ExportToJson(string path, List<DuplicateGroup> groups);
    void ExportToXml(string path, List<DuplicateGroup> groups);
    void ExportToTxt(string path, List<DuplicateGroup> groups);
}
