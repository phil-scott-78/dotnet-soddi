using System;
using System.Data.SqlClient;
using System.IO;
using System.IO.Abstractions;
using JetBrains.Annotations;

namespace Soddi.Services
{
    [UsedImplicitly]
    public class DatabaseHelper
    {
        private readonly IFileSystem _fileSystem;
        public DatabaseHelper(IFileSystem fileSystem) => _fileSystem = fileSystem;


        /// <summary>
        /// Takes a connection string and returns two new connection strings. The first sets the
        /// initial database to master and the second one points to a specific database
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        /// <exception cref="SoddiException"></exception>
        public (string master, string database) GetMasterAndDbConnectionStrings(string connectionString,
            string databaseName)
        {
            try
            {
                var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }
                    .ConnectionString;

                var database = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName }
                    .ConnectionString;

                return (master, database);
            }
            catch (ArgumentException e) when (e.Message.StartsWith(
                "Format of the initialization string does not conform to specification starting",
                StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SoddiException("Could not parse connection string");
            }
        }

        /// <summary>
        /// Given a potentially empty database name and a path figure out what to call
        /// a database
        /// </summary>
        /// <param name="databaseName">Can be empty</param>
        /// <param name="path">Path to archive. Can be a folder or a file.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public string GetDbNameFromPathOption(string? databaseName, string path)
        {
            if (!string.IsNullOrWhiteSpace(databaseName))
            {
                return databaseName;
            }

            if (_fileSystem.Directory.Exists(path))
            {
                return _fileSystem.DirectoryInfo.FromDirectoryName(path).Name;
            }

            if (_fileSystem.File.Exists(path))
            {
                return _fileSystem.Path.GetFileNameWithoutExtension(path);
            }

            // we should have already verified the path is good at this point
            // so this isn't an application exception
            throw new FileNotFoundException("Database archive path not found", path);
        }
    }
}
