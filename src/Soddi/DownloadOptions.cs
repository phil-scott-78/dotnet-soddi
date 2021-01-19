using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Soddi.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Soddi
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class DownloadOptions : CommandSettings
    {
        [CommandArgument(0, "<ARCHIVE_NAME>")]
        [Description("Archive to download")]
        public string Archive { get; set; } = "";

        [CommandOption("-o|--output")]
        [Description("Output folder")]
        public string? Output { get; set; }

        [CommandOption("-p|--pick")]
        [Description("Pick from a list of archives to download")]
        public bool Pick { get; set; }

        public static readonly string[][] Examples =
        {
            new[] { "download", "iota" }, new[] { "download", "iota", "-o", "\"/data/\"" },
            new[] { "download", "spa", "-p" }
        };
    }

    [UsedImplicitly]
    public class DownloadHandler : AsyncCommand<DownloadOptions>
    {
        private readonly IFileSystem _fileSystem;

        public DownloadHandler(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }


        public override async Task<int> ExecuteAsync(CommandContext context, DownloadOptions request)
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

            var availableArchiveParser = new AvailableArchiveParser();
            var archiveUrl =
                await availableArchiveParser.FindOrPickArchive(request.Archive, request.Pick, cancellationToken);

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn { CompletedText = Emoji.Known.CheckMark }, new DownloadedColumn(),
                    new FixedTaskDescriptionColumn(Math.Clamp(AnsiConsole.Width, 40, 65)), new ProgressBarColumn(),
                    new PercentageColumn(), new TransferSpeedColumn(), new RemainingTimeColumn(),
                }).StartAsync(async ctx =>
                {
                    List<(ProgressTask Task, Archive.UriWithSize UriWithSize)> tasks = archiveUrl.Uris
                        .Select(uriWithSize => (ctx.AddTask(uriWithSize.Description(false)), uriWithSize))
                        .ToList();

                    while (!ctx.IsFinished)
                    {
                        foreach (var (task, uriWithSize) in tasks)
                        {
                            var progress = new Progress<(int downloadedInBytes, int totalSizeInBytes)>(i =>
                                {
                                    var progressTask = task;
                                    var (downloadedInBytes, totalSizeInBytes) = i;

                                    progressTask.Increment(downloadedInBytes);
                                    progressTask.MaxValue(totalSizeInBytes);
                                }
                            );

                            var downloader = new ArchiveDownloader(outputPath, progress);
                            await downloader.Go(uriWithSize.Uri, cancellationToken);
                        }
                    }
                });

            return await Task.FromResult(0);
        }
    }
}
