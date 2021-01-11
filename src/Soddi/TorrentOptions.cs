using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using MonoTorrent;
using MonoTorrent.Client;
using Soddi.Services;
using Spectre.Console;

namespace Soddi
{
    [Verb("torrent", HelpText = "[bold red]Experimental[/]. Download database via BitTorrent"), UsedImplicitly]
    public class TorrentOptions : IRequest<int>
    {
        public TorrentOptions(string archive, string output, bool enablePortForwarding, bool pick)
        {
            Archive = archive;
            Output = output;
            EnablePortForwarding = enablePortForwarding;
            Pick = pick;
        }

        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; }

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; }

        [Option('f', "portForward", HelpText = "[red]Experimental[/]. Enable port forwarding", Default = false)]
        public bool EnablePortForwarding { get; }

        [Option('p', "pick", HelpText = "Pick from a list of archives to download", Default = false)]
        public bool Pick { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Download files associated with the math site from the torrent file",
                    new TorrentOptions("math", "", false, false));
                yield return new Example("Download to a specific folder",
                    new TorrentOptions("math", "c:\\torrent files", false, false));
                yield return new Example("Enable port forwarding",
                    new TorrentOptions("math", "", true, false));
                yield return new Example("Pick from archives containing \"stack\"",
                    new TorrentOptions("stack", "", false, true));
            }
        }
    }

    public class TorrentHandler : IRequestHandler<TorrentOptions, int>
    {
        private readonly IFileSystem _fileSystem;

        public TorrentHandler(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public async Task<int> Handle(TorrentOptions request, CancellationToken cancellationToken)
        {
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
