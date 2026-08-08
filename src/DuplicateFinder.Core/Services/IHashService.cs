namespace DuplicateFinder.Core.Services;

public interface IHashService
{
    string ComputeQuickHash(string filePath);
    string ComputeFullHash(string filePath, string algorithm);
    bool ConfirmBinaryEquality(string filePath1, string filePath2);
}
