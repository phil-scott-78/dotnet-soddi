using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Soddi.Services;

namespace Soddi.Tasks.SqlServer
{
    public class InsertData : ITask
    {
        private readonly string _connectionString;
        private readonly string _dbName;
        private readonly IArchivedDataProcessor _processor;

        // file size and rows from the aviation database to guess our way
        // to about how far we are through a file based on it's size and number of rows
        // we've read
        private static readonly Dictionary<string, decimal> s_approxSizePerRow = new Dictionary<string, decimal>()
        {
            {"badges.xml", 112},
            {"comments.xml", 341},
            {"posthistory.xml", 982},
            {"postlinks.xml", 111},
            {"posts.xml", 1522},
            {"tags.xml", 78},
            {"users.xml", 447},
            {"votes.xml", 90},
        };

        public InsertData(string connectionString, string dbName, IArchivedDataProcessor processor)
        {
            _connectionString = connectionString;
            _dbName = dbName;
            _processor = processor;
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

                    // someone smarter than me might be able to figure out how to decrypt on one thread
                    // and bulk insert on another.
                    inserter.Insert(blockingStream.AsDataReader(fileName), fileName);
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
