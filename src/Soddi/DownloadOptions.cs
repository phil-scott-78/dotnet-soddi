using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using MediatR;
using ShellProgressBar;
using Soddi;
using Soddi.Pipelines;

namespace Soddi
{
    [Verb("download", HelpText = "Download database")]
    public class DownloadOptions : IRequest<int>
    {
        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; set; } = string.Empty;

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; set; } = string.Empty;
    }
}

public class DownloadHandler : IRequestHandler<DownloadOptions, int>
{
    public async Task<int> Handle(DownloadOptions request, CancellationToken cancellationToken)
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

        var parser = new AvailableArchiveParser();
        var results = await parser.Get(cancellationToken);
        var archiveUrl = results
            .FirstOrDefault(i => i.ShortName == request.Archive ||
                                 i.LongName == request.Archive ||
                                 i.ShortName.Contains($"{request.Archive}-"));

        if (archiveUrl == null || archiveUrl.Uris.Count == 0)
        {
            throw new SoddiException($"Could not find archive named {request.Archive}");
        }

        var masterProgress = new ProgressBar(
            (int)(archiveUrl.Uris.Sum(i => i.SizeInBytes) / 1024),
            $"Downloading {request.Archive}",
            ConsoleColor.Blue);

        var allDownloaded = 0;
        var tasks = new List<Task>();
        Parallel.ForEach(archiveUrl.Uris, uri =>
        {
            var child = masterProgress.Spawn((int)(uri.SizeInBytes / 1024), uri.Uri.AbsolutePath,
                new ProgressBarOptions() {CollapseWhenFinished = true});

            var childDownloaded = 0;
            var progress = new Progress<(int downloadedInKb, int totalSizeInKb )>(i =>
            {
                var (downloaded, totalSize) = i;

                childDownloaded += downloaded;
                Interlocked.Add(ref allDownloaded, downloaded);

                child.Tick(childDownloaded,
                    $"Downloading {archiveUrl.ShortName} ({childDownloaded.KiloBytesToString()} of {totalSize.KiloBytesToString()})");
                masterProgress.Tick(allDownloaded);
            });

            var downloader = new ArchiveDownloader(outputPath, progress);
            var task = downloader.Go(uri.Uri, cancellationToken);
            tasks.Add(task);
        });

        Task.WaitAll(tasks.ToArray());
        return await Task.FromResult(0);
    }
}
