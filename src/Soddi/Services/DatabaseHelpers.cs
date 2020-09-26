using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using JetBrains.Annotations;

namespace Soddi.Services
{
    [UsedImplicitly]
    public class DatabaseHelpers
    {
        private readonly IFileSystem _fileSystem;
        public DatabaseHelpers(IFileSystem fileSystem) => _fileSystem = fileSystem;

        public IArchivedDataProcessor VerifyAndCreateProcessor(string requestPath)
        {
            if (_fileSystem.File.Exists(requestPath))
            {
                var fileInfo = _fileSystem.FileInfo.FromFileName(requestPath);

                // quick check on the extension, we'll verify contents later
                if (fileInfo.Extension.Equals(".7z", StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    throw new SoddiException("Only 7z archive files are supported");
                }

                return new ArchiveProcessor(new[] {requestPath}, _fileSystem);
            }

            if (!_fileSystem.Directory.Exists(requestPath))
            {
                throw new SoddiException("Could not find folder or archive " + requestPath);
            }

            var expectedFiles = new[]
            {
                "badges", "comments", "posthistory", "postlinks", "posts", "tags", "users", "votes"
            };
            // if they passed in a directory let's figure out if it's a collection
            // of xml files or maybe a collection of .7z files
            var directory = _fileSystem.DirectoryInfo.FromDirectoryName(requestPath);
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
                return new FolderProcessor(requestPath, _fileSystem);
            }

            if (sevenFiles.Length > 0)
            {
                AssertProperFiles(expectedFiles, sevenFiles, "7z");
                return new ArchiveProcessor(sevenFiles.Select(i => i.FullName).ToArray(), _fileSystem);
            }

            throw new SoddiException(
                "Folder doesn't appear to contain any data files. All .xml or .7z files are required to exist for processing.");
        }

        private void AssertProperFiles(IEnumerable<string> expectedFiles, IFileInfo[] foundFiles,
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

        public (string master, string database) GetMasterAndDbConnectionStrings(string connectionString,
            string databaseName)
        {
            var master = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = "master"}
                .ConnectionString;

            var database = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = databaseName}
                .ConnectionString;

            return (master, database);
        }

        public string GetDbNameFromPathOption(string databaseName, string path)
        {
            string dbName;
            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                dbName = databaseName;
            }
            else
            {
                if (_fileSystem.Directory.Exists(path))
                {
                    dbName = _fileSystem.DirectoryInfo.FromDirectoryName(path).Name;
                }
                else if (_fileSystem.File.Exists(path))
                {
                    dbName = _fileSystem.Path.GetFileNameWithoutExtension(path);
                }
                else
                {
                    // we should have already verified the path is good at this point
                    // so this isn't an application exception
                    throw new FileNotFoundException("Database archive path not found", path);
                }
            }

            return dbName;
        }
    }
}
