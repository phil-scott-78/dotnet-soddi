using System;
using System.IO;
using System.IO.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Soddi.Services
{
    public class ArchiveDownloader
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _outputPath;
        private readonly IProgress<(int downloadedInKb, int totalSizeInKb)> _progress;

        public ArchiveDownloader(string outputPath, IProgress<(int downloadedInKb, int totalSizeInKb)> progress,
            IFileSystem? fileSystem = null)
        {
            _outputPath = outputPath;
            _progress = progress;
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task Go(Uri uri, CancellationToken cancellationToken)
        {
            var filename = _fileSystem.Path.Combine(_outputPath, _fileSystem.Path.GetFileName(uri.LocalPath));

            var client = new HttpClient();
            using var response =
                await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();

            var allReadsInKb = (int)(response.Content.Headers.ContentLength / 1024 ?? 0);

            const int BufferSize = 81920;

            var buffer = new byte[BufferSize];
            var isMoreToRead = true;

            await using var fileStream = _fileSystem.FileStream.Create(filename, FileMode.Create, FileAccess.Write,
                FileShare.None, BufferSize, true);

            do
            {
                var read = await contentStream
                    .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    .ConfigureAwait(false);

                if (read != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    _progress.Report((read / 1024, allReadsInKb));
                }
                else
                {
                    isMoreToRead = false;
                    _progress.Report((allReadsInKb, allReadsInKb));
                }
            } while (isMoreToRead);
        }
    }
}
