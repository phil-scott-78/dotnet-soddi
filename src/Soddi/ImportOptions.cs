using Soddi.ProgressBar;
using Soddi.Providers;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.Core;

namespace Soddi;

public class ImportOptions : BaseLoggingOptions
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
    public string ConnectionString { get; set; } = "Server=.;Integrated Security=true;TrustServerCertificate=True";

    [CommandOption("--provider")]
    [Description("Database provider to use (sqlserver, postgres, cosmos). Default: sqlserver")]
    public string Provider { get; set; } = "sqlserver";

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

    [CommandOption("--blockSize")]
    [Description("Advanced. Block size used for concurrent read/write of 7z archives.")]
    public int BlockSize { get; set; } = 1024;

    [CommandOption("--sequential")]
    [Description("Advanced. Force sequential import of files from the same 7z archive.")]
    public bool Sequential { get; set; }

    public static readonly string[][] Examples =
    [
        ["import", "iota"], ["import", "iota.stackexchange.co.7z", "--dropAndCreate"],
        ["import", "/data/iota.stackexchange.co/", "--skipTags"]
    ];
}

record ImportSummary(long XmlRowsRead, long CountFromDb);

public class ImportHandler(
    ProviderFactory providerFactory,
    ProcessorFactory processorFactory,
    IFileSystem fileSystem,
    AvailableArchiveParser availableArchiveParser,
    ImportOptionsValidator validator,
    IAnsiConsole console)
    : AsyncCommand<ImportOptions>
{
    private async Task<string> CheckAndFixupPath(string path, CancellationToken token)
    {
        if (fileSystem.File.Exists(path) || fileSystem.Directory.Exists(path))
        {
            return path;
        }

        var archives = await availableArchiveParser.Get(token);
        var foundByShortName =
            archives.FirstOrDefault(i => i.ShortName.Equals(path, StringComparison.InvariantCultureIgnoreCase));

        if (foundByShortName != null)
        {
            var potentialLongFileName = foundByShortName.LongName + ".7z";
            if (fileSystem.File.Exists(potentialLongFileName)) return potentialLongFileName;
        }

        throw new SoddiException("Could not find archive " + path);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ImportOptions request)
    {
        var cancellationToken = CancellationToken.None;

        // Validate all options before starting import
        await validator.ValidateAsync(request, cancellationToken);

        var requestPath = await CheckAndFixupPath(request.Path, cancellationToken);

        // Get the database provider based on the --provider flag
        var providerType = ProviderFactory.ParseProviderType(request.Provider);
        var provider = providerFactory.GetProvider(providerType);

        // Use provider methods to handle connection strings and database naming
        var dbName = provider.GetDbNameFromPathOption(request.DatabaseName, requestPath);
        var masterConnectionString = provider.GetMasterConnectionString(request.ConnectionString);
        var databaseConnectionString = provider.GetDatabaseConnectionString(request.ConnectionString, dbName);

        var tasks = new Queue<(string name, ITask task)>();

        var report = new ConcurrentDictionary<string, ImportSummary>();

        var processor = processorFactory.VerifyAndCreateProcessor(requestPath, request.Sequential);

        // Get provider-specific services
        var schemaManager = providerFactory.GetSchemaManager(providerType);
        var dataInserter = providerFactory.GetDataInserter(providerType);
        var typeValueInserter = providerFactory.GetTypeValueInserter(providerType);
        var dataValidator = providerFactory.GetDataValidator(providerType);

        if (request.DropAndRecreate)
        {
            tasks.Enqueue(("Create new database", new CreateDatabaseTask(provider, masterConnectionString, dbName)));
        }
        else
        {
            tasks.Enqueue(("Verify database exists", new VerifyDatabaseExistsTask(provider, masterConnectionString, dbName)));
        }

        tasks.Enqueue(("Create schema", new CreateSchemaTask(provider, schemaManager, databaseConnectionString, !request.SkipTags)));

        tasks.Enqueue(("Insert type values", new InsertTypeValuesTask(provider, typeValueInserter, fileSystem, databaseConnectionString)));
        tasks.Enqueue(("Insert data from archive",
            new InsertDataTask(
                dataInserter,
                fileSystem,
                databaseConnectionString,
                processor,
                !request.SkipTags,
                (filename, count) => { report.TryAdd(fileSystem.Path.GetFileNameWithoutExtension(filename), new ImportSummary(count, 0)); })));

        // Add constraints and foreign keys AFTER data is loaded
        // This is critical for PostgreSQL where COPY respects FK constraints
        // (unlike SQL Server's SqlBulkCopy which can bypass them)
        if (!request.SkipPrimaryKeys)
        {
            tasks.Enqueue(("Add constraints", new AddConstraintsTask(provider, schemaManager, databaseConnectionString, request.SkipPrimaryKeys)));
            tasks.Enqueue(("Add foreign keys", new AddForeignKeysTask(provider, schemaManager, databaseConnectionString)));
        }

        tasks.Enqueue(("Check DB Status", new CheckCountsTask(
            provider,
            dataValidator,
            databaseConnectionString,
            (filename, count) => report.AddOrUpdate(filename,
                _ => new ImportSummary(0, count),
                (_, summary) => summary with { CountFromDb = count }))));

        var progressBar = console.Progress()
            .AutoClear(false)
            .Columns(new SpinnerColumn { CompletedText = Emoji.Known.CheckMark }, new FixedTaskDescriptionColumn(40),
                new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn());

        var stopWatch = Stopwatch.StartNew();

        await progressBar.StartAsync(async ctx =>
        {
            foreach (var (description, task) in tasks)
            {
                var progressTasks = new ConcurrentDictionary<string, ProgressTask>();
                var progress = new Progress<(string taskId, string message, double weight, double maxValue)>(i =>
                {
                    var (taskId, message, weight, maxValue) = i;
                    var progressBarTask = progressTasks.GetOrAdd(taskId, _ => ctx.AddTask(description));

                    progressBarTask.MaxValue(maxValue);
                    progressBarTask.Increment(weight);
                    progressBarTask.Description(message);
                });

                await task.GoAsync(progress, cancellationToken);

                foreach (var progressTask in progressTasks.Values)
                {
                    progressTask.Increment(progressTask.MaxValue - progressTask.Value);
                }
            }
        });

        stopWatch.Stop();

        var counter = 1;
        var chart = new BreakdownChart()
            .Compact()
            .Width(60)
            .UseValueFormatter(d => d.ToMetric())
            .FullSize()
            .AddItems(report,
                pair => new BreakdownChartItem(pair.Key, pair.Value.CountFromDb, counter++)
            );

        console.MarkupLine("[blue]Rows inserted[/]");
        console.Write(chart);

        if (report.Any(i => i.Value.CountFromDb != i.Value.XmlRowsRead))
        {
            var table = new Table()
                .RoundedBorder()
                .AddColumns("File", "XML Rows Read", "Count from DB");

            foreach (var (key, (xmlRowsRead, countFromDb)) in report)
            {
                var style = xmlRowsRead != countFromDb ? new Style(Color.Red) : Style.Plain;

                table.AddRow(
                    new Text(key),
                    new Text(xmlRowsRead.ToString()),
                    new Markup(countFromDb.ToString(), style)
                );
            }

            console.WriteLine();
            console.MarkupLine("[red]Mismatch Row Count[/]");
            console.Write(table);
        }

        console.WriteLine();
        console.MarkupLine($"Import complete in [blue]{stopWatch.Elapsed.Humanize(2)}[/].");

        return 0;
    }
}
