using SharpCompress.Archives.SevenZip;

namespace Soddi.Services;

public class ArchiveProcessor : IArchivedDataProcessor
{
    private readonly IFileSystem _fileSystem;
    private readonly string[] _paths;

    public ArchiveProcessor(string[] paths, IFileSystem? fileSystem = null)
    {
        _paths = paths;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public IEnumerable<(string fileName, Stream stream, int size)> GetFiles()
    {
        foreach (var path in _paths)
        {
            var stream = _fileSystem.File.OpenRead(path);
            var archive =
                SevenZipArchive.Open(stream);
            var allArchiveEntries = archive.ExtractAllEntries();

            while (allArchiveEntries.MoveToNextEntry())
            {
                if (allArchiveEntries.Entry.IsDirectory)
                {
                    continue;
                }

                var filename = allArchiveEntries.Entry.Key.ToLowerInvariant();
                var entryStream = allArchiveEntries.OpenEntryStream();

                yield return (
                    filename,
                    entryStream,
                    (int)Math.Min(int.MaxValue, allArchiveEntries.Entry.Size)
                );
            }
        }
    }

    public long GetTotalFileSize()
    {
        var sum = 0L;
        foreach (var path in _paths)
        {
            using var archive = SevenZipArchive.Open(path);
            sum += archive.TotalUncompressSize;
        }

        return sum;
    }
}

public class FolderProcessor : IArchivedDataProcessor
{
    private readonly IFileSystem _fileSystem;
    private readonly string _path;

    public FolderProcessor(string path, IFileSystem? fileSystem = null)
    {
        _path = path;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public IEnumerable<(string fileName, Stream stream, int size)> GetFiles()
    {
        var files = _fileSystem.Directory.GetFiles(_path, "*.xml");
        foreach (var file in files)
        {
            var fileInfo = _fileSystem.FileInfo.FromFileName(file);
            yield return (_fileSystem.Path.GetFileName(file).ToLowerInvariant(), fileInfo.OpenRead(),
                (int)Math.Min(int.MaxValue, fileInfo.Length));
        }
    }

    public long GetTotalFileSize()
    {
        return _fileSystem.Directory.GetFiles(_path, "*.xml").Select(i => _fileSystem.FileInfo.FromFileName(i))
            .Sum(i => i.Length);
    }
}