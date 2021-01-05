using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;
using Spectre.Console;

namespace Soddi
{
    [Verb("import", HelpText = "Import data from a 7z archive or folder"), UsedImplicitly]
    public class ImportOptions : IRequest<int>
    {
        public ImportOptions(string path, string databaseName, string connectionString, bool dropAndRecreate,
            bool skipPrimaryKeys, bool skipTags)
        {
            Path = path;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            DropAndRecreate = dropAndRecreate;
            SkipPrimaryKeys = skipPrimaryKeys;
            SkipTags = skipTags;
        }

        [Value(0, Required = true, MetaName = "Path",
            HelpText =
                "File or folder containing xml archives. The file must be a .7z file. If using a folder it can contain either .7z or .xml content")]
        public string Path { get; }

        [Option('d', "database",
            HelpText = "Name of database. If omitted the name of the file or folder will be used.")]
        public string DatabaseName { get; }

        [Option('c', "connectionString",
            HelpText =
                "Connection string to server. Initial catalog will be ignored and the database parameter will be used for the database name.",
            Default = "Server=.;Integrated Security=true")]
        public string ConnectionString { get; }

        [Option("dropAndCreate",
            HelpText =
                "Drop and recreate database. If a database already exists with this name it will be dropped. Then a database will be created in the default server file location with the default server options.",
            Default = false)]
        public bool DropAndRecreate { get; }

        [Option("skipConstraints", HelpText = "Skip adding primary keys and unique constraints.", Default = false)]
        public bool SkipPrimaryKeys { get; }

        [Option("skipTags", HelpText = "Skip adding PostTags table.", Default = false)]
        public bool SkipTags { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Import data using defaults",
                    new ImportOptions("math.stackexchange.com.7z", "", "", false, false, false));
                yield return new Example("Import data using a connection string and database name",
                    new ImportOptions("math.stackexchange.com.7z", "math",
                        "Server=(local)\\Sql2017;User Id=admin;password=t3ddy", false, false, false));
                yield return new Example("Import data using defaults and create database",
                    new ImportOptions("math.stackexchange.com.7z", "", "", true, false, false));
                yield return new Example("Import data using defaults without constraints",
                    new ImportOptions("math.stackexchange.com.7z", "", "", false, true, false));
            }
        }
    }


    public class ImportHandler : IRequestHandler<ImportOptions, int>
    {
        private readonly AvailableArchiveParser _availableArchiveParser;
        private readonly DatabaseHelper _databaseHelper;
        private readonly ProcessorFactory _processorFactory;
        private readonly IFileSystem _fileSystem;

        public ImportHandler(DatabaseHelper databaseHelper, ProcessorFactory processorFactory, IFileSystem fileSystem, AvailableArchiveParser availableArchiveParser)
        {
            _databaseHelper = databaseHelper;
            _processorFactory = processorFactory;
            _fileSystem = fileSystem;
            _availableArchiveParser = availableArchiveParser;
        }

        private async Task<string> CheckAndFixupPath(string path, CancellationToken token)
        {
            if (_fileSystem.File.Exists(path) || _fileSystem.Directory.Exists(path))
            {
                return path;
            }

            var archives = await _availableArchiveParser.Get(token);
            var foundByShortName =
                archives.FirstOrDefault(i => i.ShortName.Equals(path, StringComparison.InvariantCultureIgnoreCase));

            if (foundByShortName != null)
            {
                var potentialLongFileName = foundByShortName.LongName + ".7z";
                if (_fileSystem.File.Exists(potentialLongFileName)) return potentialLongFileName;
            }

            throw new SoddiException("Could not find archive " + path);
        }

        public async Task<int> Handle(ImportOptions request, CancellationToken cancellationToken)
        {
            var requestPath = await CheckAndFixupPath(request.Path, cancellationToken);
            var dbName = _databaseHelper.GetDbNameFromPathOption(request.DatabaseName, requestPath);
            var tasks = new Queue<(string name, ITask task)>();
            var (masterConnectionString, databaseConnectionString) =
                _databaseHelper.GetMasterAndDbConnectionStrings(request.ConnectionString, dbName);

            var processor = _processorFactory.VerifyAndCreateProcessor(requestPath);

            if (request.DropAndRecreate)
            {
                tasks.Enqueue(("Create new database", new CreateDatabase(masterConnectionString, dbName)));
            }
            else
            {
                tasks.Enqueue(("Verify database exists", new VerifyDatabaseExists(masterConnectionString, dbName)));
            }

            tasks.Enqueue(("Create schema", new CreateSchema(databaseConnectionString, !request.SkipTags)));
            if (!request.SkipPrimaryKeys)
            {
                tasks.Enqueue(("Add constraints", new AddConstraints(databaseConnectionString)));
            }

            tasks.Enqueue(("Insert type values", new InsertTypeValues(databaseConnectionString)));
            tasks.Enqueue(("Insert data from archive", new InsertData(databaseConnectionString, dbName, processor, !request.SkipTags)));

            var progressBar = AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn(),
                    new FixedTaskDescriptionColumn(40),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                });

            progressBar.Start(ctx =>
            {
                foreach (var (description, task) in tasks)
                {
                    var progressBarTask = ctx.AddTask(description);
                    progressBarTask.MaxValue(task.GetTaskWeight());
                    var progress = new Progress<(string message, int weight)>(i =>
                    {
                        var (message, weight) = i;
                        progressBarTask.Increment(weight);
                        progressBarTask.Description(message);
                    });

                    task.Go(progress);
                    progressBarTask.Increment(progressBarTask.MaxValue - progressBarTask.Value);
                }
            });

            return 0;
        }
    }
}
