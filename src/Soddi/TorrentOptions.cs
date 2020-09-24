using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using MediatR;
using MonoTorrent;
using MonoTorrent.Client;
using Spectre.Console;

namespace Soddi
{
    [Verb("torrent", HelpText = "Download database via BitTorrent")]
    public class TorrentOptions : IRequest<int>
    {
        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; set; } = string.Empty;

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; set; } = string.Empty;
    }

    public class TorrentHandler : IRequestHandler<TorrentOptions, int>
    {
        public async Task<int> Handle(TorrentOptions request, CancellationToken cancellationToken)
        {
            var outputPath = request.Output;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(outputPath))
            {
                throw new SoddiException($"Output path {outputPath} not found");
            }

            Console.WriteLine("Finding torrent...");

            var parser = new AvailableArchiveParser();
            var results = await parser.Get(cancellationToken);
            var archiveUrl = results.FirstOrDefault(i => i.ShortName == request.Archive ||
                                                         i.LongName == request.Archive ||
                                                         i.ShortName.Contains($"{request.Archive}-"));
            if (archiveUrl == null)
            {
                throw new SoddiException($"Archive named {request.Archive} not found");
            }

            const string url = "https://archive.org/download/stackexchange/stackexchange_archive.torrent";
            var httpClient = new HttpClient();
            var torrentContents = await httpClient.GetByteArrayAsync(url);

            EngineSettings settings = new EngineSettings
            {
                AllowedEncryption = EncryptionTypes.All, SavePath = outputPath
            };

            if (!Directory.Exists(settings.SavePath))
            {
                Directory.CreateDirectory(settings.SavePath);
            }

            Console.WriteLine("Initializing BitTorrent engine...");
            var engine = new ClientEngine(settings);
            Torrent torrent = await Torrent.LoadAsync(torrentContents);
            foreach (var torrentFile in torrent.Files)
            {
                if (torrentFile.Path != archiveUrl.LongName + ".7z" &&
                    torrentFile.Path.Contains(request.Archive + "-") == false)
                {
                    torrentFile.Priority = Priority.DoNotDownload;
                }
            }

            TorrentManager manager = new TorrentManager(
                torrent,
                outputPath,
                new TorrentSettings());

            await engine.Register(manager);
            await engine.StartAll();


            Console.CursorVisible = false;
            Console.Clear();

            // we don't want to clear if we don't have to. so we'll keep track of the hash values
            // of some things that will indicate whether or not we need to redraw the whole screen
            var clearHash = -1;

            while (manager.State != TorrentState.Stopped && manager.State != TorrentState.Seeding)
            {
                var newClearHash = 0;
                var tableWrapper = new Table() {Border = Border.None, ShowHeaders = false};
                tableWrapper.AddColumn("dummy");

                Console.SetCursorPosition(0, 0);

                var engineTable = new Table {ShowHeaders = false};
                engineTable.AddColumns("Desc", "Value");
                engineTable.AddRow(new Text("Total Upload Rate"),
                    new Markup($"{engine.TotalDownloadSpeed / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Total Download Rate"),
                    new Markup($"{engine.TotalUploadSpeed / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Disk Read Rate"),
                    new Markup($"{engine.DiskManager.ReadRate / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Disk Write Rate"),
                    new Markup($"{engine.DiskManager.WriteRate / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Total Read"),
                    new Markup($"{engine.DiskManager.TotalRead / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Total Written"),
                    new Markup($"{engine.DiskManager.TotalWritten / 1024.0:0.00}kB/s"));
                engineTable.AddRow(new Text("Open Connections"),
                    new Markup($"{engine.ConnectionManager.OpenConnections}"));
                engineTable.AddRow(new Text("Half Open Connections"),
                    new Markup($"{engine.ConnectionManager.HalfOpenConnections}"));


                // AnsiConsole.Render(engineTable);

                var torrentTable = new Table {ShowHeaders = false, Border = Border.Rounded};
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


                var trackerTable = new Table();
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

                newClearHash += manager.TrackerManager.Tiers.Count;

                var peerTable = new Table {Border = Border.Rounded};
                peerTable.AddColumns("Peer", "Pieces", "Download Speed", "Upload Speed");
                var peers = await manager.GetPeersAsync();
                foreach (var peerId in peers)
                {
                    peerTable.AddRow(
                        new Text(peerId.Uri.ToString()),
                        new Text(peerId.AmRequestingPiecesCount.ToString()),
                        new Text($"{peerId.Monitor.DownloadSpeed / 1024.0:0.00} kB/s"),
                        new Text($"{peerId.Monitor.UploadSpeed / 1024.0:0.00} kB/s"));
                }

                newClearHash += peers.Count * 100;

                var mainStatusTable = GetEmptyContainerTable(2);
                mainStatusTable.AddRow(torrentTable, trackerTable);
                tableWrapper.AddRow(mainStatusTable);
                tableWrapper.AddRow(peerTable);

                if (manager.Torrent != null)
                {
                    var torrentListTable = new Table {Border = Border.Rounded};
                    torrentListTable.AddColumns("Archive File", "Downloaded", "Size", "Percent");
                    foreach (var file in manager.Torrent.Files.Where(i => i.Priority != Priority.DoNotDownload))
                    {
                        torrentListTable.AddRow(
                            file.Path,
                            $"{file.BytesDownloaded / (1024.0 * 1024.0):0.00} MB",
                            $"{file.Length / (1024.0 * 1024.0):0.00} MB",
                            $"{file.BitField.PercentComplete / 100:P1}");
                    }

                    newClearHash += manager.Torrent.Files.Count(i => i.Priority != Priority.DoNotDownload) * 1000;

                    tableWrapper.AddRow(torrentListTable);
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

            Console.WriteLine("Download complete");

            return await Task.FromResult(0);
        }

        private static Table GetEmptyContainerTable(int columns = 1)
        {
            var t = new Table() {ShowHeaders = false, Border = Border.None};
            for (var i = 0; i < columns; i++)
            {
                t.AddColumn(i.ToString());
            }

            return t;
        }
    }
}
