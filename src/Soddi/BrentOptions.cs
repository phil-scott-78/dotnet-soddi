using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;

namespace Soddi;

public class BrentOptions : BaseLoggingOptions
{
    [CommandArgument(0, "[ARCHIVE_NAME]")]
    [Description("Archive to download")]
    public string? Archive { get; set; } = "";

    [CommandOption("-o|--output")]
    [Description("Output folder")]
    public string? Output { get; set; }

    [CommandOption("--skipExtraction")]
    [Description("Don't extract the downloaded 7z files")]
    public bool SkipExtraction { get; set; }

    [CommandOption("-f|--portForward")]
    [Description("[red]Experimental[/]. Enable port forwarding")]
    public bool EnablePortForwarding { get; set; }

    public static readonly string[][] Examples =
    {
        new[] { "brent" }, new[] { "brent", "small" }, new[] { "brent", "medium" }, new[] { "brent", "large" },
        new[] { "brent", "extra-large" },
    };
}

public class BrentHandler(IAnsiConsole console, TorrentDownloader torrentDownloader, IFileSystem fileSystem)
    : AsyncCommand<BrentOptions>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BrentOptions settings)
    {
        var archives = new List<BrentArchive>()
        {
            new("https://downloads.brentozar.com/StackOverflow-SQL-Server-2010.torrent",
                "Small: 10GB database as of 2010", "small"),
            new("https://downloads.brentozar.com/StackOverflow2013.torrent", "Medium: 50GB database as of 2013",
                "medium"),
            new("https://downloads.brentozar.com/StackOverflowCore.torrent", "Large 150GB database as of 2019",
                "large"),
            new("https://downloads.brentozar.com/StackOverflow-SQL-Server-202006.torrent",
                "Extra-Large: current 381GB database as of 2020/06", "extra-large"),
        };

        var archiveName = settings.Archive;
        if (string.IsNullOrWhiteSpace(archiveName))
        {
            var choice = console.Prompt(
                new SelectionPrompt<BrentArchive>()
                    .PageSize(10)
                    .Title("Pick an archive to download")
                    .AddChoices(archives));
            archiveName = choice.ShortName;
        }

        var archive = archives.FirstOrDefault(a =>
            a.ShortName.Equals(archiveName, StringComparison.InvariantCultureIgnoreCase));

        if (archive == null)
        {
            throw new SoddiException($"Could not find an archive matching \"{archiveName}\"");
        }

        var outputPath = settings.Output;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = fileSystem.Directory.GetCurrentDirectory();
        }

        if (!fileSystem.Directory.Exists(outputPath))
        {
            throw new SoddiException($"Output path {outputPath} not found");
        }

        var downloadedFiles = await torrentDownloader.DownloadAsync(archive.Url,
            settings.EnablePortForwarding = settings.EnablePortForwarding,
            outputPath,
            CancellationToken.None);

        var sevenZipFiles = downloadedFiles.Where(i =>
            fileSystem.Path.GetExtension(i).Equals(".7z", StringComparison.InvariantCultureIgnoreCase));

        var stopWatch = Stopwatch.StartNew();


        var progressBar = console.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new SpinnerColumn { CompletedText = Emoji.Known.CheckMark }, new TaskDescriptionColumn(),
                new ProgressBarColumn(), new PercentageColumn(), new TransferSpeedColumn(),
                new RemainingTimeColumn()
            });

        progressBar.Start(ctx =>
        {
            foreach (var sevenZipFile in sevenZipFiles)
            {
                using var stream = fileSystem.File.OpenRead(sevenZipFile);
                using var sevenZipArchive = SevenZipArchive.Open(stream);

                var tasks = sevenZipArchive.Entries.ToImmutableDictionary(
                    e => e.Key,
                    e => ctx.AddTask(e.Key, new ProgressTaskSettings { MaxValue = e.Size, AutoStart = false }));

                foreach (var entry in sevenZipArchive.Entries)
                {
                    var currentTask = tasks[entry.Key];
                    currentTask.StartTask();
                    var totalRead = 0L;

                    void Handler(object? sender, CompressedBytesReadEventArgs args)
                    {
                        var diff = args.CurrentFilePartCompressedBytesRead - totalRead;
                        currentTask.Increment(diff);
                        totalRead = args.CurrentFilePartCompressedBytesRead;
                    }

                    sevenZipArchive.CompressedBytesRead += Handler;
                    entry.WriteToDirectory(outputPath, new ExtractionOptions { Overwrite = true });
                    sevenZipArchive.CompressedBytesRead -= Handler;
                }
            }
        });

        stopWatch.Stop();
        console.MarkupLine($"Extraction complete in [blue]{stopWatch.Elapsed.Humanize()}[/].");

        return 0;
    }
}

public class BrentArchive(string url, string name, string shortName)
{
    public string Url { get; } = url;
    public string Name { get; } = name;
    public string ShortName { get; } = shortName;

    public override string ToString()
    {
        return Name;
    }
}
