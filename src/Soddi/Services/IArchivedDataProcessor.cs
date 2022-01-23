namespace Soddi.Services;

public interface IArchivedDataProcessor
{
    IEnumerable<IEnumerable<(string fileName, Stream stream, long size)>> GetFiles();
}
