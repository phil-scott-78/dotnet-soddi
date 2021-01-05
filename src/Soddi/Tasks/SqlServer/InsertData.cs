using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Soddi.Services;

namespace Soddi.Tasks.SqlServer
{
    public class InsertData : ITask
    {
        private readonly string _connectionString;
        private readonly string _dbName;
        private readonly IArchivedDataProcessor _processor;
        private readonly bool _includePostTags;

        // file size and rows from the aviation database to guess our way
        // to about how far we are through a file based on it's size and number of rows
        // we've read
        private static readonly Dictionary<string, decimal> s_approxSizePerRow = new Dictionary<string, decimal>()
        {
            { "badges.xml", 112 },
            { "comments.xml", 341 },
            { "posthistory.xml", 982 },
            { "postlinks.xml", 111 },
            { "posts.xml", 1522 },
            { "tags.xml", 78 },
            { "users.xml", 447 },
            { "votes.xml", 90 },
        };

        public InsertData(string connectionString, string dbName, IArchivedDataProcessor processor,
            bool includePostTags)
        {
            _connectionString = connectionString;
            _dbName = dbName;
            _processor = processor;
            _includePostTags = includePostTags;
        }

        public void Go(IProgress<(string message, int weight)> progress)
        {
            var weightPerByte = GetTaskWeight() / (decimal)_processor.GetTotalFileSize();

            foreach (var (fileName, stream, _) in _processor.GetFiles())
            {
                // ReSharper disable AccessToDisposedClosure
                using var blockingStream = new BlockingStream(1024 * 1024 * 1024);
                var decrypt = Task.Factory.StartNew(() =>
                {
                    stream.CopyTo(blockingStream);
                    blockingStream.CompleteWriting();
                });

                var insert = Task.Factory.StartNew(() =>
                {
                    var sizePerRow = s_approxSizePerRow[fileName];
                    var totalBatchCount = 0L;
                    var inserter = new SqlServerBulkInserter(_connectionString, _dbName, l =>
                    {
                        var diff = l - totalBatchCount;
                        totalBatchCount = l;
                        var min = diff * sizePerRow * weightPerByte;
                        progress.Report(($"{fileName} ({l} rows read)", (int)min));
                    });

                    var isPostFile = fileName.Equals("posts.xml", StringComparison.InvariantCultureIgnoreCase);
                    Task? postTagTask = null;
                    PubSubPostTagDataReader? postTagDataReader = null;

                    if (isPostFile && _includePostTags)
                    {
                        // if we are reading a posts.xml file and we need to generate the tags we'll launch a third
                        // thread to bulk insert the tags as we find them. it'll use the PubSubPostTagDataReader
                        // as the source of the data, and then in the thread that is reading posts.xml when it finds
                        // a new row with tags it'll call an action (defined below) that adds that tag to the DataReader
                        postTagDataReader = new PubSubPostTagDataReader();
                        postTagTask = Task.Factory.StartNew(() =>
                        {
                            var postTagInserter = new SqlServerBulkInserter(_connectionString, _dbName, _ => { });
                            postTagInserter.Insert(postTagDataReader, "PostTags.xml");
                        });
                    }

                    IDataReader dataReader = blockingStream
                        .AsDataReader(
                            fileName,
                            postAndTag => postTagDataReader?.Push(postAndTag.postId, postAndTag.tags)
                        );

                    inserter.Insert(dataReader, fileName);

                    // if we have a post tag reader make sure we close it so the queue gets cleared out
                    // then wait for it to complete
                    postTagDataReader?.Close();
                    postTagTask?.Wait();
                });


                Task.WaitAll(decrypt, insert);

                // ReSharper restore AccessToDisposedClosure
            }
        }

        public int GetTaskWeight()
        {
            return 1_000_000;
        }
    }
}
