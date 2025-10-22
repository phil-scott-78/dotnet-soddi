using Soddi.Providers;
using Soddi.Services;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for inserting data from archive files
/// </summary>
public class InsertDataTask(
    IDataInserter dataInserter,
    IFileSystem fileSystem,
    string connectionString,
    IArchivedDataProcessor processor,
    bool includePostTags,
    Action<string, long> reportCount)
    : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress,
        CancellationToken cancellationToken)
    {
        // keep a list of the insertion tasks. we want to move on to reading
        // as quick as possible as the backlog of inserts is taking care of
        await Parallel.ForEachAsync(processor.GetFiles(), cancellationToken, async (batch, token) =>
        {
            batch = batch.ToList();
            foreach (var (fileName, stream, fileSize) in batch)
            {
                PubSubPostTagDataReader? postTagDataReader = null;
                var isPostFile = fileName.Equals("posts.xml", StringComparison.InvariantCultureIgnoreCase);

                if (isPostFile && includePostTags)
                {
                    // if we are reading a posts.xml file and we need to generate the tags we'll launch a third
                    // thread to bulk insert the tags as we find them. it'll use the PubSubPostTagDataReader
                    // as the source of the data, and then in the thread that is reading posts.xml when it finds
                    // a new row with tags it'll call an action (defined below) that adds that tag to the DataReader
                    postTagDataReader = new PubSubPostTagDataReader();
                }

                // the blocking stream will let us read and write simultaneously
                var blockingStream = new BlockingStream();

                var totalBatchCount = 0L;
                var tableName = fileSystem.Path.GetFileNameWithoutExtension(fileName);

                // Create progress wrapper that handles the complex progress reporting
                var inserterProgress = new Progress<double>(rowsCopied =>
                {
                    var sizePerRow = blockingStream.TotalBytesRead / rowsCopied;
                    var estRowsPerFile = fileSize / sizePerRow;
                    var diff = rowsCopied - totalBatchCount;
                    totalBatchCount = (long)rowsCopied;
                    var rowsRead = rowsCopied < int.MaxValue ? Convert.ToDouble(rowsCopied).ToMetric(decimals: 2) : "billions of";
                    progress.Report((fileName, $"{fileName} ({rowsRead} rows)", diff,
                        Math.Max(estRowsPerFile, totalBatchCount + 1)));
                });

                var decrypt = stream.CopyToAsync(blockingStream, token).ContinueWith((_, _) =>
                {
                    blockingStream.CompleteWriting();
                    postTagDataReader?.NoMoreRecords();
                }, token, token);

                Task? postTagTask = null;
                if (postTagDataReader != null)
                {
                    var postTagTableName = fileSystem.Path.GetFileNameWithoutExtension("PostTags.xml");
                    var postTagProgress = new Progress<double>(_ => { });
                    postTagTask = dataInserter.BulkInsertAsync(
                        connectionString,
                        postTagTableName,
                        postTagDataReader,
                        postTagProgress,
                        token);
                }

                var dataReader = blockingStream
                    .AsDataReader(
                        fileName,
                        postAndTag => postTagDataReader?.Push(postAndTag.postId, postAndTag.tags)
                    );

                await dataInserter.BulkInsertAsync(connectionString, tableName, dataReader, inserterProgress, token);
                await decrypt;

                // if we have a post tag reader make sure we close it so the queue gets cleared out
                // then wait for it to complete

                if (postTagTask != null)
                {
                    await postTagTask;
                }

                reportCount(fileName, dataReader.RecordsAffected);
                // up until this point we've been guessing at the total size
                // of the import so go ahead and nudge it to 100%
                progress.Report((fileName, fileName, fileSize, fileSize));
            }
        });
    }

    public double GetTaskWeight()
    {
        return 1_000_000;
    }
}
