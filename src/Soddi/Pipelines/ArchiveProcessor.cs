using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives.SevenZip;

namespace Soddi.Pipelines
{
    public class ArchiveProcessor : IArchivedDataProcessor
    {
        private readonly string[] _paths;

        public ArchiveProcessor(string[] paths)
        {
            _paths = paths;
        }

        public IEnumerable<(string fileName, Stream stream, int size)> GetFiles()
        {
            foreach (var path in _paths)
            {
                var stream = File.OpenRead(path);
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
        private readonly string _path;

        public FolderProcessor(string path)
        {
            _path = path;
        }

        public IEnumerable<(string fileName, Stream stream, int size)> GetFiles()
        {
            var files = Directory.GetFiles(_path, "*.xml");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                yield return (Path.GetFileName(file).ToLowerInvariant(), fileInfo.OpenRead(),
                    (int)Math.Min(int.MaxValue, fileInfo.Length));
            }
        }

        public long GetTotalFileSize()
        {
            return Directory.GetFiles(_path, "*.xml")
                .Select(i => new FileInfo(i))
                .Sum(i => i.Length);
        }
    }
}
