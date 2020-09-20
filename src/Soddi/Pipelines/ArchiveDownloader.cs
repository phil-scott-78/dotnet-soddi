using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Soddi.Pipelines
{
    public class ArchiveDownloader
    {
        private readonly string _outputPath;
        private readonly IProgress<(int downloadedInKb, int totalSizeInKb)> _progress;

        public ArchiveDownloader(string outputPath, IProgress<(int downloadedInKb, int totalSizeInKb)> progress)
        {
            _outputPath = outputPath;
            _progress = progress;
        }

        public async Task Go(Uri uri, CancellationToken cancellationToken)
        {
            string filename = Path.Combine(_outputPath, Path.GetFileName(uri.LocalPath));

            var client = new HttpClient();
            using var response =
                await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();

            var allReadsInKb = (int)(response.Content.Headers.ContentLength / 1024 ?? 0);

            const int bufferSize = 81920;

            var buffer = new byte[bufferSize];
            var isMoreToRead = true;

            await using var fileStream = new FileStream(
                filename,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                true);

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
                }
            } while (isMoreToRead);
        }
    }
}
