using MonoTorrent;
using MonoTorrent.Client;
using Soddi.ProgressBar;

namespace Soddi;

public class TorrentDownloader
{
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;

    public TorrentDownloader(IFileSystem fileSystem, IAnsiConsole console)
    {
        _fileSystem = fileSystem;
        _console = console;
    }

    public async Task<ImmutableList<string>> Download(string url,
        bool enablePortForwarding,
        string outputPath,
        CancellationToken cancellationToken)
    {
        return await Download(url, null, enablePortForwarding, outputPath, cancellationToken);
    }

    public async Task<ImmutableList<string>> Download(string url,
        List<string>? potentialArchives,
        bool enablePortForwarding,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var stopWatch = Stopwatch.StartNew();

        var paddingFolder = _fileSystem.Path.Combine(outputPath, ".____padding_file");
        var doesPaddingFolderExistToStart = _fileSystem.Directory.Exists(paddingFolder);

        var progressBar = _console.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new SpinnerColumn { CompletedText = Emoji.Known.CheckMark }, new DownloadedColumnExtended(),
                new TaskDescriptionColumn(), new TorrentProgressBarColumn(), new PercentageColumn(),
                new TransferSpeedColumn(), new RemainingTimeColumn()
            });

        ImmutableList<TorrentFile>? downloadedFiles = null;
        await progressBar.StartAsync(async ctx =>
        {
            _console.WriteLine("Loading torrent...");

            var httpClient = new HttpClient();
            var torrentContents = await httpClient.GetByteArrayAsync(url, cancellationToken);
            var settings = new EngineSettings
            {
                AllowedEncryption = EncryptionTypes.All, SavePath = outputPath, MaximumHalfOpenConnections = 16
            };


            _console.WriteLine("Initializing BitTorrent engine...");
            var engine = new ClientEngine(settings);

            if (enablePortForwarding)
            {
                _console.WriteLine("Attempting to forward ports");
                await engine.EnablePortForwardingAsync(cancellationToken);
            }

            var torrent = await Torrent.LoadAsync(torrentContents);
            if (potentialArchives != null)
            {
                foreach (var torrentFile in torrent.Files)
                {
                    if (!potentialArchives.Contains(torrentFile.Path))
                    {
                        torrentFile.Priority = Priority.DoNotDownload;
                    }
                }
            }

            var manager = new TorrentManager(
                torrent,
                outputPath,
                new TorrentSettings { MaximumConnections = 250 }, string.Empty);

            await engine.Register(manager);
            await engine.StartAll();


            downloadedFiles = manager.Torrent.Files
                .Where(i => i.Priority != Priority.DoNotDownload).ToImmutableList();

            var fileTasks = downloadedFiles
                .ToDictionary(
                    i => i.Path,
                    file => ctx.AddTask(file.Path,
                        new ProgressTaskSettings { MaxValue = file.Length, AutoStart = false })
                );

            while (manager.State != TorrentState.Stopped && manager.State != TorrentState.Seeding)
            {
                foreach (var torrentFile in downloadedFiles)
                {
                    var progressTask = fileTasks[torrentFile.Path];
                    if (torrentFile.BytesDownloaded > 0 && progressTask.IsStarted == false)
                    {
                        progressTask.StartTask();
                    }

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

            foreach (var progressTask in fileTasks)
            {
                progressTask.Value.StopTask();
            }
        });


        stopWatch.Stop();
        _console.MarkupLine($"Download complete in [blue]{stopWatch.Elapsed.Humanize()}[/].");

        return downloadedFiles?.Select(i => _fileSystem.Path.Combine(outputPath, i.Path)).ToImmutableList() ??
               ImmutableList<string>.Empty;
    }
}