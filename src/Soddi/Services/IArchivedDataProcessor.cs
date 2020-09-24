using System.Collections.Generic;
using System.IO;

namespace Soddi.Services
{
    public interface IArchivedDataProcessor
    {
        IEnumerable<(string fileName, Stream stream, int size)> GetFiles();
        long GetTotalFileSize();
    }
}
