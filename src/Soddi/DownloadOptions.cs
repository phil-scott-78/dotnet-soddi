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
using Soddi.Services;
using Spectre.Console;

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

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
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

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new SpinnerColumn(),
                    new FixedTaskDescriptionColumn(Math.Clamp(AnsiConsole.Width, 40, 65)),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                }).StartAsync(async ctx =>
                {
                    List<(ProgressTask Task, Archive.UriWithSize UriWithSize)> tasks = archiveUrl.Uris
                        .Select(uriWithSize => (ctx.AddTask(uriWithSize.Description()), uriWithSize))
                        .ToList();

                    while (!ctx.IsFinished)
                    {
                        foreach (var (task, uriWithSize) in tasks)
                        {
                            var progress = new Progress<(int downloadedInKb, int totalSizeInKb)>(i =>
                            {
                                long downloadedInBytes = i.downloadedInKb * 1024;
                                long totalSizeInBytes = i.totalSizeInKb * 1024;

                                task.Increment(i.downloadedInKb);
                                task.MaxValue(i.totalSizeInKb);
                                task.Description(
                                    $"{uriWithSize.Description()} - {downloadedInBytes.BytesToString()}/{totalSizeInBytes.BytesToString()}"
                                );
                            });

                            var downloader = new ArchiveDownloader(outputPath, progress);
                            await downloader.Go(uriWithSize.Uri, cancellationToken);
                        }
                    }
                });

            return await Task.FromResult(0);
        }
    }
}
