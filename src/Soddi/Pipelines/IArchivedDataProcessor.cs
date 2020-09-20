using System.Collections.Generic;
using System.IO;

namespace Soddi.Pipelines
{
    public interface IArchivedDataProcessor
    {
        IEnumerable<(string fileName, Stream stream, int size)> GetFiles();
        long GetTotalFileSize();
    }
}
