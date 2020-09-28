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
        public TorrentOptions(string archive, string output, bool enablePortForwarding)
        {
            Archive = archive;
            Output = output;
            EnablePortForwarding = enablePortForwarding;
        }

        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; }

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; }

        [Option('f', "portForward", HelpText = "[red]Experimental[/]. Enable port forwarding", Default = false)]
        public bool EnablePortForwarding { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Download files associated with the math site from the torrent file",
                    new TorrentOptions("math", "", false));
                yield return new Example("Download to a specific folder",
                    new TorrentOptions("math", "c:\\torrent files", false));
                yield return new Example("Enable port forwarding",
                    new TorrentOptions("math", "", true));
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

            Console.WriteLine("Finding archive files...");

            var parser = new AvailableArchiveParser();
            var results = await parser.Get(cancellationToken);
            var archiveUrl = results.FirstOrDefault(i => i.ShortName == request.Archive ||
                                                         i.LongName == request.Archive ||
                                                         i.ShortName.Contains($"{request.Archive}-"));
            if (archiveUrl == null)
            {
                throw new SoddiException($"Archive named {request.Archive} not found");
            }

            Console.WriteLine("Loading torrent...");
            const string Url = "https://archive.org/download/stackexchange/stackexchange_archive.torrent";
            var httpClient = new HttpClient();
            var torrentContents = await httpClient.GetByteArrayAsync(Url);


            var settings = new EngineSettings
            {
                AllowedEncryption = EncryptionTypes.All,
                SavePath = outputPath
            };

            Console.WriteLine("Initializing BitTorrent engine...");
            var engine = new ClientEngine(settings);

            if (request.EnablePortForwarding)
            {
                Console.WriteLine("Attempting to forward ports");
                await engine.EnablePortForwardingAsync(cancellationToken);

                // This is how to access the list of port mappings, and to see if they were
                // successful, pending or failed. If they failed it could be because the public port
                // is already in use by another computer on your network.
                foreach (var successfulMapping in engine.PortMappings.Created)
                {
                    Console.WriteLine(
                        $"  Mapped {successfulMapping.Protocol}: {successfulMapping.PrivatePort}->{successfulMapping.PublicPort}");
                }

                foreach (var failedMapping in engine.PortMappings.Failed)
                {
                    Console.WriteLine(
                        $"  Failed mapping {failedMapping.Protocol}: {failedMapping.PrivatePort}->{failedMapping.PublicPort}");
                }

                foreach (var failedMapping in engine.PortMappings.Pending)
                {
                    Console.WriteLine(
                        $"  Pending but probably failed mapping {failedMapping.Protocol}: {failedMapping.PrivatePort}->{failedMapping.PublicPort}");
                }
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

            Console.CursorVisible = false;
            Console.Clear();

            // we don't want to clear if we don't have to. so we'll keep track of some values of what is on
            // screen so we only refresh when we need to
            var clearHash = -1;

            while (manager.State != TorrentState.Stopped && manager.State != TorrentState.Seeding)
            {
                var newClearHash = 0;


                Console.SetCursorPosition(0, 0);

                var torrentTable = BuildTorrentOverviewTable(manager);
                var trackerTable = BuildTrackerTable(manager);

                newClearHash += manager.TrackerManager.Tiers.Count;

                var peers = await manager.GetPeersAsync();
                var peerTable = BuildPeerTable(peers);
                newClearHash += peers.Count * 100;


                var tableWrapper = GetEmptyContainerTable();
                var mainStatusTable = GetEmptyContainerTable(2);
                mainStatusTable.AddRow(torrentTable, trackerTable);

                tableWrapper.AddRow(mainStatusTable);
                tableWrapper.AddRow(peerTable);

                if (manager.Torrent != null)
                {
                    var torrentListTable = BuildTorrentListTable(manager);
                    tableWrapper.AddRow(torrentListTable);

                    newClearHash += manager.Torrent.Files.Count(i => i.Priority != Priority.DoNotDownload) * 1000;
                }

                if (newClearHash != clearHash)
                {
                    // there is some combination of new trackers, urls and peers
                    // so the screen might look funky so we'll wipe it
                    clearHash = newClearHash;
                    Console.Clear();
                }

                AnsiConsole.Render(tableWrapper);
                Thread.Sleep(500);
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

            Console.WriteLine("Download complete");
            return await Task.FromResult(0);
        }

        private static Table BuildTorrentListTable(TorrentManager manager)
        {
            var torrentListTable = new Table { Border = Border.Rounded };
            torrentListTable.AddColumns("Archive File", "Downloaded", "Size", "Percent");
            foreach (var file in manager.Torrent.Files.Where(i => i.Priority != Priority.DoNotDownload))
            {
                torrentListTable.AddRow(
                    file.Path,
                    $"{file.BytesDownloaded / (1024.0 * 1024.0):0.00} MB",
                    $"{file.Length / (1024.0 * 1024.0):0.00} MB",
                    $"{file.BitField.PercentComplete / 100:P1}");
            }

            return torrentListTable;
        }

        private static Table BuildTorrentOverviewTable(TorrentManager manager)
        {
            var torrentTable = new Table { ShowHeaders = false, Border = Border.Rounded };
            torrentTable.AddColumns("Desc", "Value");
            torrentTable.AddRow("State", manager.State.ToString());
            torrentTable.AddRow("Name", manager.Torrent == null ? "MetaDataMode" : manager.Torrent.Name);
            // torrentTable.AddRow("Progress", manager.Progress.ToString(CultureInfo.InvariantCulture));
            torrentTable.AddRow("Download Speed", $"{manager.Monitor.DownloadSpeed / 1024.0:0.00} kB/s");
            torrentTable.AddRow("Upload Speed", $"{manager.Monitor.UploadSpeed / 1024.0:0.00} kB/s");

            torrentTable.AddRow("Total Downloaded",
                $"{manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0):0.00} MB");
            torrentTable.AddRow("Total Uploaded",
                $"{manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0):0.00} MB");
            return torrentTable;
        }

        private static Table BuildTrackerTable(TorrentManager manager)
        {
            var trackerTable = new Table { Border = Border.Rounded };
            trackerTable.AddColumns("Tracker", "Last announce");
            foreach (var trackerManagerTier in manager.TrackerManager.Tiers)
            {
                var activeTracker = trackerManagerTier.ActiveTracker.ToString();
                if (activeTracker != null)
                {
                    trackerTable.AddRow(new Text(activeTracker),
                        new Text(trackerManagerTier.LastAnnounceSucceeded.ToString()));
                }
            }

            return trackerTable;
        }

        private static Table BuildPeerTable(IEnumerable<PeerId> peers)
        {
            var peerTable = new Table { Border = Border.Rounded };
            peerTable.AddColumns("Peer", "Pieces", "Download Speed", "Upload Speed");
            foreach (var peerId in peers)
            {
                peerTable.AddRow(
                    new Text(peerId.Uri.ToString()),
                    new Text(peerId.AmRequestingPiecesCount.ToString()),
                    new Text($"{peerId.Monitor.DownloadSpeed / 1024.0:0.00} kB/s"),
                    new Text($"{peerId.Monitor.UploadSpeed / 1024.0:0.00} kB/s"));
            }

            return peerTable;
        }

        private static Table GetEmptyContainerTable(int columns = 1)
        {
            var t = new Table() { ShowHeaders = false, Border = Border.None };
            for (var i = 0; i < columns; i++)
            {
                t.AddColumn(i.ToString());
            }

            return t;
        }
    }
}
