using Soddi.Services;

namespace Soddi;

public class TorrentOptions : BaseLoggingOptions
{
    [CommandArgument(0, "<ARCHIVE_NAME>")]
    [Description("Archive to download")]
    public string Archive { get; set; } = "";

    [CommandOption("-o|--output")]
    [Description("Output folder")]
    public string? Output { get; set; }

    [CommandOption("-f|--portForward")]
    [Description("[red]Experimental[/]. Enable port forwarding")]
    public bool EnablePortForwarding { get; set; }

    [CommandOption("-p|--pick")]
    [Description("Pick from a list of archives to download")]
    public bool Pick { get; set; }

    public static readonly string[][] Examples =
    [
        ["torrent", "iota"], ["torrent", "iota", "-o", "/data/"],
        ["torrent", "spa", "-p", "-f"]
    ];
}

public class TorrentHandler(IFileSystem fileSystem, IAnsiConsole console,
    AvailableArchiveParser availableArchiveParser)
    : AsyncCommand<TorrentOptions>
{
    public override async Task<int> ExecuteAsync(CommandContext context, TorrentOptions request)
    {
        var cancellationToken = CancellationToken.None;

        var outputPath = request.Output;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = fileSystem.Directory.GetCurrentDirectory();
        }

        if (!fileSystem.Directory.Exists(outputPath))
        {
            throw new SoddiException($"Output path {outputPath} not found");
        }

        console.WriteLine("Finding archive files...");

        var archiveUrls =
            await availableArchiveParser.FindOrPickArchive(request.Archive, request.Pick, cancellationToken);


        var potentialArchives = new List<string>();
        foreach (var archiveUrl in archiveUrls)
        {
            potentialArchives.AddRange([
                archiveUrl.LongName + ".7z", $"{archiveUrl.LongName}-Badges.7z",
                $"{archiveUrl.LongName}-Comments.7z", $"{archiveUrl.LongName}-PostHistory.7z",
                $"{archiveUrl.LongName}-PostLinks.7z", $"{archiveUrl.LongName}-Posts.7z",
                $"{archiveUrl.LongName}-Tags.7z", $"{archiveUrl.LongName}-Users.7z",
                $"{archiveUrl.LongName}-Votes.7z"
            ]);
        }

        const string Url = "https://archive.org/download/stackexchange/stackexchange_archive.torrent";
        var torrentDownloader = new TorrentDownloader(fileSystem, console);

        await torrentDownloader.DownloadAsync(Url, potentialArchives, request.EnablePortForwarding, outputPath,
            cancellationToken);
        return await Task.FromResult(0);
    }
}
