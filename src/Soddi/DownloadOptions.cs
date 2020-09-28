using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using ShellProgressBar;
using Soddi.Services;

// ReSharper disable AccessToDisposedClosure

namespace Soddi
{
    [Verb("download", HelpText = "Download the most recent data dump for a Stack Overflow site from archive.org"),
     UsedImplicitly]
    public class DownloadOptions : IRequest<int>
    {
        public DownloadOptions(string archive, string output)
        {
            Archive = archive;
            Output = output;
        }

        [Value(0, HelpText = "Archive to download", Required = true, MetaName = "Archive")]
        public string Archive { get; }

        [Option('o', "output", HelpText = "Output folder")]
        public string Output { get; }

        [Usage(ApplicationAlias = "soddi")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("download archive for aviation.stackexchange.com",
                    new DownloadOptions("aviation", ""));
                yield return new Example("download archive for math.stackexchange.com to a particular folder",
                    new DownloadOptions("math", "c:\\stack-data"));
            }
        }
    }

    public class DownloadHandler : IRequestHandler<DownloadOptions, int>
    {
        private readonly IFileSystem _fileSystem;

        public DownloadHandler(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public async Task<int> Handle(DownloadOptions request, CancellationToken cancellationToken)
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

            using var masterProgress = new ProgressBar(
                (int)(archiveUrl.Uris.Sum(i => i.SizeInBytes) / 1024),
                $"Downloading {request.Archive}",
                ConsoleColor.Blue);

            var allDownloaded = 0;
            var tasks = new List<Task>();
            Parallel.ForEach(archiveUrl.Uris, uri =>
            {
                var child = masterProgress.Spawn((int)(uri.SizeInBytes / 1024), uri.Uri.AbsolutePath,
                    new ProgressBarOptions() { CollapseWhenFinished = true });

                var childDownloaded = 0;
                var progress = new Progress<(int downloadedInKb, int totalSizeInKb)>(i =>
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
            masterProgress.Tick(masterProgress.MaxTicks, "Done");
            return await Task.FromResult(0);
        }
    }
}
