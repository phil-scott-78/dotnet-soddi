namespace Soddi.Services;

[UsedImplicitly]
public class ProcessorFactory(IFileSystem fileSystem)
{
    public IArchivedDataProcessor VerifyAndCreateProcessor(string requestPath, bool processSequentially = false)
    {
        if (fileSystem.File.Exists(requestPath))
        {
            var fileInfo = fileSystem.FileInfo.FromFileName(requestPath);

            // quick check on the extension, we'll verify contents later
            if (fileInfo.Extension.Equals(".7z", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                throw new SoddiException("Only 7z archive files are supported");
            }

            return processSequentially
                ? new SequentialArchiveProcessor(new[] { requestPath }, fileSystem)
                : new ParallelArchiveProcessor(new[] { requestPath }, fileSystem);
        }

        if (!fileSystem.Directory.Exists(requestPath))
        {
            throw new SoddiException("Could not find folder or archive " + requestPath);
        }

        var expectedFiles = new[]
        {
            "badges", "comments", "posthistory", "postlinks", "posts", "tags", "users", "votes"
        };
        // if they passed in a directory let's figure out if it's a collection
        // of xml files or maybe a collection of .7z files
        var directory = fileSystem.DirectoryInfo.FromDirectoryName(requestPath);
        var xmlFiles = directory.GetFiles("*.xml").ToArray();
        var sevenFiles = directory.GetFiles("*.7z").ToArray();
        if (sevenFiles.Length > 0 && xmlFiles.Length > 0)
        {
            throw new SoddiException(
                "Both 7z and xml files exist in folder. Only folders with one type is supported.");
        }

        if (xmlFiles.Length > 0)
        {
            AssertProperFiles(expectedFiles, xmlFiles, "xml");
            return new FolderProcessor(requestPath, fileSystem);
        }

        if (sevenFiles.Length > 0)
        {
            AssertProperFiles(expectedFiles, sevenFiles, "7z");
            var files = sevenFiles.Select(i => i.FullName).ToArray();
            return processSequentially
                ? new SequentialArchiveProcessor(files, fileSystem)
                : new ParallelArchiveProcessor(files, fileSystem);
        }

        throw new SoddiException(
            "Folder doesn't appear to contain any data files. All .xml or .7z files are required to exist for processing.");
    }


    private static void AssertProperFiles(IEnumerable<string> expectedFiles, IFileInfo[] foundFiles,
        string extension)
    {
        var missing = expectedFiles
            .Where(i => !foundFiles.Any(f => f.Name.Contains(i, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new SoddiException("Directory found, but missing data archives for: " +
                                     string.Join(", ",
                                         missing.Select(missingFile => $"\"{missingFile}.{extension}\"")));
        }
    }
}
