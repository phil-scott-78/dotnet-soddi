using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Soddi
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class ImportOptions : CommandSettings
    {
        [CommandArgument(0, "<PATH>")]
        [Description(
            "File or folder containing xml archives. The file must be a .7z file. If using a folder it can contain either .7z or .xml content")]
        public string Path { get; set; } = "";

        [CommandOption("-d|--database")]
        [Description("Name of database. If omitted the name of the file or folder will be used.")]
        public string? DatabaseName { get; set; }

        [CommandOption("-c|--connectionString")]
        [Description(
            "Connection string to server. Initial catalog will be ignored and the database parameter will be used for the database name.")]
        public string ConnectionString { get; set; } = "Server=.;Integrated Security=true";

        [CommandOption("--dropAndCreate")]
        [Description(
            "Drop and recreate database. If a database already exists with this name it will be dropped. Then a database will be created in the default server file location with the default server options.")]
        public bool DropAndRecreate { get; set; }

        [CommandOption("--skipConstraints")]
        [Description("Skip adding primary keys and unique constraints.")]
        public bool SkipPrimaryKeys { get; set; }

        [CommandOption("--skipTags")]
        [Description("Skip adding PostTags table.")]
        public bool SkipTags { get; set; }
    }


    public class ImportHandler : AsyncCommand<ImportOptions>
    {
        private readonly AvailableArchiveParser _availableArchiveParser;
        private readonly DatabaseHelper _databaseHelper;
        private readonly ProcessorFactory _processorFactory;
        private readonly IFileSystem _fileSystem;

        public ImportHandler(DatabaseHelper databaseHelper, ProcessorFactory processorFactory, IFileSystem fileSystem,
            AvailableArchiveParser availableArchiveParser)
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

        public override async Task<int> ExecuteAsync(CommandContext context, ImportOptions request)
        {
            var cancellationToken = CancellationToken.None;

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
            tasks.Enqueue(("Insert data from archive",
                new InsertData(databaseConnectionString, dbName, processor, !request.SkipTags)));

            var progressBar = AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn(), new FixedTaskDescriptionColumn(40), new ProgressBarColumn(),
                    new PercentageColumn(), new RemainingTimeColumn(),
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
