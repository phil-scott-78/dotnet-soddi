using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using JetBrains.Annotations;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Soddi
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class BrentOptions : CommandSettings
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

    public class BrentHandler : AsyncCommand<BrentOptions>
    {
        private readonly IAnsiConsole _console;
        private readonly TorrentDownloader _torrentDownloader;
        private readonly IFileSystem _fileSystem;

        public BrentHandler(IAnsiConsole console, TorrentDownloader torrentDownloader, IFileSystem fileSystem)
        {
            _console = console;
            _torrentDownloader = torrentDownloader;
            _fileSystem = fileSystem;
        }


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
                var choice = _console.Prompt(
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
                outputPath = _fileSystem.Directory.GetCurrentDirectory();
            }

            if (!_fileSystem.Directory.Exists(outputPath))
            {
                throw new SoddiException($"Output path {outputPath} not found");
            }

            var downloadedFiles = await _torrentDownloader.Download(archive.Url, settings.EnablePortForwarding,
                outputPath,
                CancellationToken.None);

            var sevenZipFiles = downloadedFiles.Where(i =>
                _fileSystem.Path.GetExtension(i).Equals(".7z", StringComparison.InvariantCultureIgnoreCase));

            var stopWatch = Stopwatch.StartNew();


            var progressBar = _console.Progress()
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
                    using var stream = _fileSystem.File.OpenRead(sevenZipFile);
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
            _console.MarkupLine($"Extraction complete in [blue]{stopWatch.Elapsed.Humanize()}[/].");

            return 0;
        }
    }

    public class BrentArchive
    {
        public BrentArchive(string url, string name, string shortName)
        {
            Url = url;
            Name = name;
            ShortName = shortName;
        }

        public string Url { get; }
        public string Name { get; }
        public string ShortName { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
