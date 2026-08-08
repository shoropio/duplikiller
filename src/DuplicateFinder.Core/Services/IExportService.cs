using DuplicateFinder.Core.Models;

namespace DuplicateFinder.Core.Services;

public interface IExportService
{
    void ExportToCsv(string path, List<DuplicateGroup> groups);
    void ExportToJson(string path, List<DuplicateGroup> groups);
    void ExportToXml(string path, List<DuplicateGroup> groups);
    void ExportToTxt(string path, List<DuplicateGroup> groups);
}
