using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using MediatR;
using ShellProgressBar;
using Soddi.Pipelines;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;

namespace Soddi
{
    [Verb("create")]
    public class CreateOptions : IRequest<int>
    {
        [Value(0, Required = true, MetaName = "Path", HelpText = "File or folder containing xml archive")]
        public string Path { get; set; } = string.Empty;

        [Option('d', "database", HelpText = "Database name to create. Will be file name by default")]
        public string DatabaseName { get; set; } = string.Empty;

        [Option('c', "connectionString", HelpText = "Connection string to server",
            Default = "Server=.;Integrated Security=true")]
        public string ConnectionString { get; set; } = "Server=.;Integrated Security=true";

        [Option("dropAndCreate", HelpText = "Drop and recreate database in default file location",
            Default = false)]
        public bool DropAndRecreate { get; set; } = false;

        [Option('p', "primaryKeys", HelpText = "Add primary keys and unique constraints", Default = true)]
        public bool PrimaryKeys { get; set; } = true;
    }

    public class CreateHandler : IRequestHandler<CreateOptions, int>
    {
        public Task<int> Handle(CreateOptions request, CancellationToken cancellationToken)
        {
            var dbName = GetDbNameFromPathOption(request);
            var tasks = new Queue<(string name, ITask task)>();
            var (masterConnectionString, databaseConnectionString) =
                GetMasterAndDbConnectionStrings(request.ConnectionString, dbName);

            var processor = VerifyAndCreateProcessor(request.Path);

            if (request.DropAndRecreate)
            {
                tasks.Enqueue(("Create new database", new CreateDatabase(masterConnectionString, dbName)));
            }

            tasks.Enqueue(("Create schema", new CreateSchema(databaseConnectionString)));
            if (request.PrimaryKeys)
            {
                tasks.Enqueue(("Add constraints", new AddConstraints(databaseConnectionString)));
            }

            tasks.Enqueue(("Insert type values", new InsertTypeValues(databaseConnectionString)));
            tasks.Enqueue(("Insert data from archive", new InsertData(databaseConnectionString, dbName, processor)));

            var maxTicks = tasks.Sum(i => i.task.GetTaskWeight());
            var progressBar = new ProgressBar(maxTicks, "Running", ConsoleColor.Blue);
            var progressCount = 0;
            var tickCountShouldBe = 0;

            var progress = new Progress<(string message, int weight)>(i =>
            {
                var (message, weight) = i;

                var progressBarCurrentTick = progressBar.CurrentTick + weight;

                // if we get ahead then let's keep displaying value that's high
                // and let the current value catch up. we shouldn't be too off.
                if (progressBarCurrentTick > tickCountShouldBe)
                {
                    progressBar.Tick(progressBarCurrentTick, message);
                }
                else
                {
                    progressBar.Tick(tickCountShouldBe, message);
                }
            });


            foreach (var (_, task) in tasks)
            {
                task.Go(progress);

                // we are just guessing at progress when using IProgress based on some
                // average sizes so catch up if we get ahead
                tickCountShouldBe += task.GetTaskWeight();
                if (progressCount < tickCountShouldBe)
                {
                    progressCount = tickCountShouldBe;
                    progressBar.Tick(progressCount);
                }
            }

            progressBar.Tick(progressCount, "Done");
            return Task.FromResult(0);
        }

        private IArchivedDataProcessor VerifyAndCreateProcessor(string requestPath)
        {
            if (File.Exists(requestPath))
            {
                if (new FileInfo(requestPath).Extension.Equals("7z", StringComparison.InvariantCultureIgnoreCase) ==
                    false)
                {
                    throw new SoddiException("Only 7z archive files are supported");
                }

                return new ArchiveProcessor(new[] {requestPath});
            }

            if (!Directory.Exists(requestPath))
            {
                throw new SoddiException("Could not find folder or archive " + requestPath);
            }

            var expectedFiles = new[]
            {
                "badges", "comments", "posthistory", "postlinks", "posts", "tags", "users", "votes"
            };
            // if they passed in a directory let's figure out if it's a collection
            // of xml files or maybe a collection of .7z files
            var directory = new DirectoryInfo(requestPath);
            var xmlFiles = directory.GetFiles("*.xml").ToArray();
            var sevenFiles = directory.GetFiles("*.7z").ToArray();
            if (sevenFiles.Length > 0 && xmlFiles.Length > 0)
            {
                throw new SoddiException(
                    "Both 7z and xml files exist in folder. Only folders with one type is supported.");
            }

            if (xmlFiles.Length > 0)
            {
                var missing = expectedFiles.Where(i =>
                    !xmlFiles.Any(xml => xml.Name.Contains(i, StringComparison.InvariantCultureIgnoreCase))).ToList();
                if (missing.Count > 0)
                {
                    throw new SoddiException("Directory found, but missing data files for: " +
                                             missing.Select(missingFile => $"\"{missingFile}.xml\""));
                }

                return new FolderProcessor(requestPath);
            }

            if (sevenFiles.Length > 0)
            {
                var missing = expectedFiles.Where(i =>
                        !sevenFiles.Any(seven => seven.Name.Contains(i, StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();
                if (missing.Count > 0)
                {
                    throw new SoddiException("Directory found with 7z files, but missing 7z archives for: " +
                                             string.Join(", ",
                                                 missing.Select(missingFile => $"\"{missingFile}.xml\"")));
                }

                return new ArchiveProcessor(sevenFiles.Select(i => i.FullName).ToArray());
            }

            throw new SoddiException(
                "Folder doesn't appear to contain any data files. All .xml or .7z files are required to exist for processing.");
        }

        private static (string master, string database) GetMasterAndDbConnectionStrings(string connectionString,
            string databaseName)
        {
            var master = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = "master"}
                .ConnectionString;

            var database = new SqlConnectionStringBuilder(connectionString) {InitialCatalog = databaseName}
                .ConnectionString;

            return (master, database);
        }

        private static string GetDbNameFromPathOption(CreateOptions request)
        {
            string dbName;
            if (!string.IsNullOrWhiteSpace(request.DatabaseName))
            {
                dbName = request.DatabaseName;
            }
            else
            {
                if (Directory.Exists(request.Path))
                {
                    dbName = new DirectoryInfo(request.Path).Name;
                }
                else if (File.Exists(request.Path))
                {
                    dbName = Path.GetFileNameWithoutExtension(request.Path);
                }
                else
                {
                    // we should have already verified the path is good at this point
                    // so this isn't an application exception
                    throw new FileNotFoundException("Database archive path not found", request.Path);
                }
            }

            return dbName;
        }
    }
}
