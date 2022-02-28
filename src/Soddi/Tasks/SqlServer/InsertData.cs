using Soddi.Services;

namespace Soddi.Tasks.SqlServer;

public class InsertData : ITask
{
    private readonly string _connectionString;
    private readonly string _dbName;
    private readonly IArchivedDataProcessor _processor;
    private readonly bool _includePostTags;
    private readonly Action<string, long> _reportCount;

    public InsertData(string connectionString, string dbName, IArchivedDataProcessor processor,
        bool includePostTags, Action<string, long> reportCount)
    {
        _connectionString = connectionString;
        _dbName = dbName;
        _processor = processor;
        _includePostTags = includePostTags;
        _reportCount = reportCount;
    }

    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress,
        CancellationToken cancellationToken)
    {
        // keep a list of the insertion tasks. we want to move on to reading
        // as quick as possible as the backlog of inserts is taking care of
        await Parallel.ForEachAsync(_processor.GetFiles(), cancellationToken, async (batch, token) =>
        {
            batch = batch.ToList();
            foreach (var (fileName, stream, fileSize) in batch)
            {
                PubSubPostTagDataReader? postTagDataReader = null;
                var isPostFile = fileName.Equals("posts.xml", StringComparison.InvariantCultureIgnoreCase);

                if (isPostFile && _includePostTags)
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
                var inserter = new SqlServerBulkInserter(_connectionString, _dbName, l =>
                {
                    var sizePerRow = (double)blockingStream.TotalBytesRead / l;
                    var estRowsPerFile = fileSize / sizePerRow;
                    var diff = l - totalBatchCount;
                    totalBatchCount = l;
                    var rowsRead = l < int.MaxValue ? Convert.ToDouble(l).ToMetric(decimals: 2) : "billions of";
                    progress.Report((fileName, $"{fileName} ({rowsRead} rows)", diff,
                        Math.Max(estRowsPerFile, totalBatchCount + 1)));
                });

                var decrypt = stream.CopyToAsync(blockingStream, token).ContinueWith((task, o) =>
                {
                    blockingStream.CompleteWriting();
                    postTagDataReader?.NoMoreRecords();
                }, token, token);

                Task? postTagTask = null;
                if (postTagDataReader != null)
                {
                    var postTagInserter = new SqlServerBulkInserter(_connectionString, _dbName, _ => { });
                    postTagTask = postTagInserter.InsertAsync(postTagDataReader, "PostTags.xml", cancellationToken);
                }

                var dataReader = blockingStream
                    .AsDataReader(
                        fileName,
                        postAndTag => postTagDataReader?.Push(postAndTag.postId, postAndTag.tags)
                    );

                await inserter.InsertAsync(dataReader, fileName, cancellationToken);
                await decrypt;

                // if we have a post tag reader make sure we close it so the queue gets cleared out
                // then wait for it to complete

                if (postTagTask != null)
                {
                    await postTagTask;
                }

                _reportCount(fileName, dataReader.RecordsAffected);
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
