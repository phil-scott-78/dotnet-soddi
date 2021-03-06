﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Threading.Tasks;
using Humanizer;
using Soddi.Services;

namespace Soddi.Tasks.SqlServer
{
    public class InsertData : ITask
    {
        private readonly string _connectionString;
        private readonly string _dbName;
        private readonly IArchivedDataProcessor _processor;
        private readonly bool _includePostTags;
        private readonly Action<ImmutableDictionary<string, long>> _summaryReporter;
        private readonly int _blockSize;

        // file size and rows from the aviation database to guess our way
        // to about how far we are through a file based on it's size and number of rows
        // we've read
        private static readonly Dictionary<string, double> s_approxSizePerRow = new()
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
            bool includePostTags, Action<ImmutableDictionary<string, long>> summaryReporter, int blockSize = 1024)
        {
            _connectionString = connectionString;
            _dbName = dbName;
            _processor = processor;
            _includePostTags = includePostTags;
            _summaryReporter = summaryReporter;
            _blockSize = blockSize;
        }

        public void Go(IProgress<(string taskId, string message, double weight, double maxValue)> progress)
        {
            // keep a list of the insertion tasks. we want to move on to reading
            // as quick as possible as the backlog of inserts is taking care of
            var insertTasks = new List<Task>();
            var fileReport = new ConcurrentDictionary<string, long>();
            foreach (var (fileName, stream, fileSize) in _processor.GetFiles())
            {
                // the blocking stream will let us read and write simultaneously
                var blockingStream = new BlockingStream(_blockSize);
                var decrypt = Task.Factory.StartNew(() =>
                {
                    stream.CopyTo(blockingStream);
                    blockingStream.CompleteWriting();
                });

                var insert = Task.Factory.StartNew(() =>
                {
                    var sizePerRow = s_approxSizePerRow[fileName];
                    var estRowsPerFile = fileSize / sizePerRow;
                    var totalBatchCount = 0L;
                    var inserter = new SqlServerBulkInserter(_connectionString, _dbName, l =>
                    {
                        var diff = l - totalBatchCount;
                        totalBatchCount = l;
                        fileReport.AddOrUpdate(fileName, _ => l, (_, _) => l);
                        string rowsRead = l < int.MaxValue ? Convert.ToDouble(l).ToMetric(decimals: 2) : "billions of";
                        progress.Report((fileName, $"{fileName} ({rowsRead} rows)", diff, estRowsPerFile));
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

                    // up until this point we've been guessing at the total size
                    // of the import so go ahead and nudge it to 100%
                    progress.Report((fileName, fileName, fileSize, fileSize));
                });

                // add the insertion task to be completed in the background, but make sure
                // we wait for decryption to complete before moving on because the 7z files
                // can't be decrypted in parallel
                insertTasks.Add(insert);
                decrypt.Wait();
            }

            _summaryReporter(fileReport.ToImmutableDictionary());
            Task.WaitAll(insertTasks.ToArray());
        }

        public double GetTaskWeight()
        {
            return 1_000_000;
        }
    }
}
