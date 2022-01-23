using SharpCompress.Archives.SevenZip;

namespace Soddi.Services;

public class ParallelArchiveProcessor : IArchivedDataProcessor
{
    private readonly IFileSystem _fileSystem;
    private readonly string[] _paths;

    public ParallelArchiveProcessor(string[] paths, IFileSystem? fileSystem = null)
    {
        _paths = paths;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public IEnumerable<IEnumerable<(string fileName, Stream stream, long size)>> GetFiles()
    {
        foreach (var path in _paths)
        {
            var allFiles = SevenZipArchive.Open(path).Entries.Select(i => i.Key);
            foreach (var file in allFiles)
            {
                yield return Batch(path, file);
            }
        }

        IEnumerable<(string fileName, Stream stream, long size)> Batch(string path, string entryFile)
        {
            var stream = _fileSystem.File.OpenRead(path);
            var archive = SevenZipArchive.Open(stream);
            var allArchiveEntries = archive.Entries.First(i => i.Key == entryFile);
            yield return (
                allArchiveEntries.Key.ToLowerInvariant(),
                allArchiveEntries.OpenEntryStream(),
                allArchiveEntries.Size);
        }
    }
}

public class SequentialArchiveProcessor : IArchivedDataProcessor
{
    private readonly IFileSystem _fileSystem;
    private readonly string[] _paths;

    public SequentialArchiveProcessor(string[] paths, IFileSystem? fileSystem = null)
    {
        _paths = paths;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public IEnumerable<IEnumerable<(string fileName, Stream stream, long size)>> GetFiles()
    {
        foreach (var path in _paths)
        {
            yield return Batch(path);
        }

        IEnumerable<(string fileName, Stream stream, long size)> Batch(string path)
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
                    allArchiveEntries.Entry.Size
                );
            }
        }
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

    public IEnumerable<IEnumerable<(string fileName, Stream stream, long size)>> GetFiles()
    {
        var files = _fileSystem.Directory.GetFiles(_path, "*.xml");
        foreach (var file in files)
        {
            yield return Batch(file);
        }

        IEnumerable<(string fileName, Stream stream, long size)> Batch(string path)
        {
            var fileInfo = _fileSystem.FileInfo.FromFileName(path);
            yield return (_fileSystem.Path.GetFileName(path).ToLowerInvariant(), fileInfo.OpenRead(), fileInfo.Length);
        }
    }
}
