namespace Soddi.Services;

public class ArchiveDownloader(string outputPath, IProgress<(int downloadedInKb, int totalSizeInKb)> progress,
    IFileSystem? fileSystem = null)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

    public async Task GoAsync(Uri uri, CancellationToken cancellationToken)
    {
        var filename = _fileSystem.Path.Combine(outputPath, _fileSystem.Path.GetFileName(uri.LocalPath));

        var client = new HttpClient();
        using var response = await client
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var allReadsInBytes = (int)(response.Content.Headers.ContentLength ?? 0);

        const int BufferSize = 1024 * 1024;

        var buffer = new byte[BufferSize];
        var isMoreToRead = true;

        await using var fileStream = _fileSystem
            .FileStream
            .New(filename, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        do
        {
            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

            if (read != 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                progress.Report((read, allReadsInBytes));
            }
            else
            {
                isMoreToRead = false;
                progress.Report((allReadsInBytes, allReadsInBytes));
            }
        } while (isMoreToRead);
    }
}
