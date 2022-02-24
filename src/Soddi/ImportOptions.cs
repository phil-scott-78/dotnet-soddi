using Soddi.ProgressBar;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;

namespace Soddi;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
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
    {
        new[] { "import", "iota" }, new[] { "import", "iota.stackexchange.co.7z", "--dropAndCreate" },
        new[] { "import", "/data/iota.stackexchange.co/", "--skipTags" },
    };
}

record ImportSummary(long XmlRowsRead, long CountFromDb);

public class ImportHandler : AsyncCommand<ImportOptions>
{
    private readonly AvailableArchiveParser _availableArchiveParser;
    private readonly DatabaseHelper _databaseHelper;
    private readonly ProcessorFactory _processorFactory;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;

    public ImportHandler(DatabaseHelper databaseHelper, ProcessorFactory processorFactory, IFileSystem fileSystem,
        AvailableArchiveParser availableArchiveParser, IAnsiConsole console)
    {
        _databaseHelper = databaseHelper;
        _processorFactory = processorFactory;
        _fileSystem = fileSystem;
        _availableArchiveParser = availableArchiveParser;
        _console = console;
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
        var cancellationToken = new CancellationToken();

        var requestPath = await CheckAndFixupPath(request.Path, cancellationToken);
        var dbName = _databaseHelper.GetDbNameFromPathOption(request.DatabaseName, requestPath);
        var tasks = new Queue<(string name, ITask task)>();
        var (masterConnectionString, databaseConnectionString) =
            _databaseHelper.GetMasterAndDbConnectionStrings(request.ConnectionString, dbName);

        var report = new ConcurrentDictionary<string, ImportSummary>();

        var processor = _processorFactory.VerifyAndCreateProcessor(requestPath, request.Sequential);

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
            tasks.Enqueue(("Add foreign keys", new AddForeignKeys(databaseConnectionString, !request.SkipTags)));
        }

        tasks.Enqueue(("Insert type values", new InsertTypeValues(databaseConnectionString)));
        tasks.Enqueue(("Insert data from archive",
            new InsertData(
                databaseConnectionString,
                dbName,
                processor,
                !request.SkipTags, (filename, count) => { report.TryAdd(_fileSystem.Path.GetFileNameWithoutExtension(filename), new ImportSummary(count, 0)); })));

        tasks.Enqueue(("Check DB Status", new CheckCounts(
            databaseConnectionString,
            (filename, count) => report.AddOrUpdate(filename,
                _ => new ImportSummary(0, count),
                (_, summary) => summary with { CountFromDb = count }))));

        var progressBar = _console.Progress()
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

        _console.MarkupLine("[blue]Rows inserted[/]");
        _console.Write(chart);

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

            _console.WriteLine();
            _console.MarkupLine("[red]Mismatch Row Count[/]");
            _console.Write(table);
        }

        _console.WriteLine();
        _console.MarkupLine($"Import complete in [blue]{stopWatch.Elapsed.Humanize(2)}[/].");

        return 0;
    }
}
