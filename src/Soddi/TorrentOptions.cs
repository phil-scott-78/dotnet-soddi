using System;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MonoTorrent;
using MonoTorrent.Client;
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
                    new SpinnerColumn(), new FixedTaskDescriptionColumn(Math.Clamp(AnsiConsole.Width, 40, 65)),
                    new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn()
                });

            AnsiConsole.WriteLine("Finding archive files...");

            var availableArchiveParser = new AvailableArchiveParser();
            var archiveUrl =
                await availableArchiveParser.FindOrPickArchive(request.Archive, request.Pick, cancellationToken);

            await progressBar.StartAsync(async ctx =>
            {
                AnsiConsole.WriteLine("Loading torrent...");
                const string Url = "https://archive.org/download/stackexchange/stackexchange_archive.torrent";
                var httpClient = new HttpClient();
                var torrentContents = await httpClient.GetByteArrayAsync(Url, cancellationToken);
                var settings = new EngineSettings { AllowedEncryption = EncryptionTypes.All, SavePath = outputPath };

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
                    if (torrentFile.Path != archiveUrl.LongName + ".7z" &&
                        torrentFile.Path.Contains(request.Archive + "-") == false)
                    {
                        torrentFile.Priority = Priority.DoNotDownload;
                    }
                }

                var manager = new TorrentManager(
                    torrent,
                    outputPath,
                    new TorrentSettings(), string.Empty);

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
                        progressTask.Description(
                            $"{torrentFile.Path} - {torrentFile.BytesDownloaded.BytesToString()}/{torrentFile.Length.BytesToString()}");
                        progressTask.Increment(torrentFile.BytesDownloaded - progressTask.Value);
                    }

                    Thread.Sleep(100);
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

            AnsiConsole.WriteLine("Download complete");
            return await Task.FromResult(0);
        }
    }
}
