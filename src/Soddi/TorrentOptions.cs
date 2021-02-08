using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using JetBrains.Annotations;
using MonoTorrent;
using MonoTorrent.Client;
using Soddi.ProgressBar;
using Soddi.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Soddi
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class TorrentOptions : CommandSettings
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
        {
            new[] { "torrent", "iota" }, new[] { "torrent", "iota", "-o", "/data/" },
            new[] { "torrent", "spa", "-p", "-f" }
        };
    }

    public class TorrentHandler : AsyncCommand<TorrentOptions>
    {
        private readonly IFileSystem _fileSystem;

        public TorrentHandler(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, TorrentOptions request)
        {
            var cancellationToken = CancellationToken.None;

            var outputPath = request.Output;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = _fileSystem.Directory.GetCurrentDirectory();
            }

            if (!_fileSystem.Directory.Exists(outputPath))
            {
                throw new SoddiException($"Output path {outputPath} not found");
            }

            var paddingFolder = _fileSystem.Path.Combine(outputPath, ".____padding_file");
            var doesPaddingFolderExistToStart = _fileSystem.Directory.Exists(paddingFolder);

            var progressBar = AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn { CompletedText = Emoji.Known.CheckMark }, new DownloadedColumn(),
                    new TaskDescriptionColumn(), new TorrentProgressBarColumn(), new PercentageColumn(),
                    new TransferSpeedColumn(), new RemainingTimeColumn()
                });

            AnsiConsole.WriteLine("Finding archive files...");

            var availableArchiveParser = new AvailableArchiveParser();
            var archiveUrls =
                await availableArchiveParser.FindOrPickArchive(request.Archive, request.Pick, cancellationToken);


            var potentialArchives = new List<string>();
            foreach (var archiveUrl in archiveUrls)
            {
                potentialArchives.AddRange(new[]
                {
                    archiveUrl.LongName + ".7z", $"{archiveUrl.LongName}-Badges.7z",
                    $"{archiveUrl.LongName}-Comments.7z", $"{archiveUrl.LongName}-PostHistory.7z",
                    $"{archiveUrl.LongName}-PostLinks.7z", $"{archiveUrl.LongName}-Posts.7z",
                    $"{archiveUrl.LongName}-Tags.7z", $"{archiveUrl.LongName}-Users.7z",
                    $"{archiveUrl.LongName}-Votes.7z"
                });
            }

            var stopWatch = Stopwatch.StartNew();

            await progressBar.StartAsync(async ctx =>
            {
                AnsiConsole.WriteLine("Loading torrent...");
                const string Url = "https://archive.org/download/stackexchange/stackexchange_archive.torrent";
                var httpClient = new HttpClient();
                var torrentContents = await httpClient.GetByteArrayAsync(Url, cancellationToken);
                var settings = new EngineSettings
                {
                    AllowedEncryption = EncryptionTypes.All, SavePath = outputPath, MaximumHalfOpenConnections = 16
                };


                AnsiConsole.WriteLine("Initializing BitTorrent engine...");
                var engine = new ClientEngine(settings);

                if (request.EnablePortForwarding)
                {
                    AnsiConsole.WriteLine("Attempting to forward ports");
                    await engine.EnablePortForwardingAsync(cancellationToken);
                }

                var torrent = await Torrent.LoadAsync(torrentContents);
                foreach (var torrentFile in torrent.Files)
                {
                    if (!potentialArchives.Contains(torrentFile.Path))
                    {
                        torrentFile.Priority = Priority.DoNotDownload;
                    }
                }

                var manager = new TorrentManager(
                    torrent,
                    outputPath,
                    new TorrentSettings { MaximumConnections = 250 }, string.Empty);

                await engine.Register(manager);
                await engine.StartAll();

                var fileTasks = manager.Torrent.Files
                    .Where(i => i.Priority != Priority.DoNotDownload)
                    .ToDictionary(
                        i => i.Path,
                        file => ctx.AddTask(file.Path, new ProgressTaskSettings { MaxValue = file.Length })
                    );

                while (manager.State != TorrentState.Stopped && manager.State != TorrentState.Seeding)
                {
                    foreach (var torrentFile in manager.Torrent.Files.Where(i => i.Priority != Priority.DoNotDownload))
                    {
                        var progressTask = fileTasks[torrentFile.Path];
                        progressTask.Increment(torrentFile.BytesDownloaded - progressTask.Value);
                        progressTask.State.Update<BitSmuggler>("torrentBits",
                            _ => new BitSmuggler(torrentFile.BitField));
                    }

                    await Task.Delay(100, cancellationToken);
                }

                await manager.StopAsync();
                await engine.StopAllAsync();

                try
                {
                    // the stackoverflow torrent files, and I think all of archive.org
                    // seem to have these padding files that sneak into the download even
                    // if they aren't included in the file list. not quite sure how to prevent that
                    // so I'm gonna delete them after the fact I guess
                    if (!doesPaddingFolderExistToStart && _fileSystem.Directory.Exists(paddingFolder))
                    {
                        _fileSystem.Directory.Delete(paddingFolder, true);
                    }
                }
                catch
                {
                    /* swallow */
                }
            });

            stopWatch.Stop();
            AnsiConsole.MarkupLine($"Download complete in [blue]{stopWatch.Elapsed.Humanize()}[/].");
            return await Task.FromResult(0);
        }
    }
}
