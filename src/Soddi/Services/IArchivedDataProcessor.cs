namespace Soddi.Services;

public interface IArchivedDataProcessor
{
    IEnumerable<(string fileName, Stream stream, long size)> GetFiles();
}
