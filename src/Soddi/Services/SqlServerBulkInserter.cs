using System.Buffers;
using Microsoft.Data.SqlClient;
using Polly;

namespace Soddi.Services;

public class SqlServerBulkInserter
{
    private readonly IFileSystem _fileSystem;
    private readonly string _connectionString;
    private readonly string _dbName;
    private readonly Action<long> _rowsCopied;

    public SqlServerBulkInserter(
        string connectionString,
        string dbName,
        Action<long> rowsCopied,
        IFileSystem? fileSystem = null)
    {
        _connectionString = connectionString;
        _dbName = dbName;
        _rowsCopied = rowsCopied;
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public void Insert(IDataReader dataReader, string fileName)
    {
        var tableName = _fileSystem.Path.GetFileNameWithoutExtension(fileName);
        var connBuilder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = _dbName };

        // posts and posthistory are significantly larger due to the text content so we need
        // to make sure their batches are smaller than the other tables. Comments we'll tweak a bit lower too.
        var batchSize = fileName.ToLowerInvariant() switch
        {
            "posts.xml" => 500,
            "posthistory.xml" => 500,
            "comments.xml" => 2000,
            _ => 10_000
        };

        // the buffered data reader will keep track of the past few batches we've written in case of failure. If we
        // get a network glitch or timeout we don't want to have to retry, especially with some of these data sets being
        // nearly 100gb worth of data. But if we can pause reading from the incoming stream and see which data from our
        // buffer has been written we can hopefully get the data into a good place once we've reconnected and can start
        // pushing data again.
        var bufferedStream = new BufferedDataReader(dataReader, batchSize);

        var retryPolicy = Policy
            .Handle<SqlException>()
            .WaitAndRetryForever(_ => TimeSpan.FromMilliseconds(500), (_, _, _) =>
                {
                    // if have an exception we want to tell the buffered stream to start
                    // replaying the buffered messages before we start reading from the incoming stream.
                    bufferedStream.SetReplay();
                }
            );

        long totalCopiedSoFar = 0;
        retryPolicy.Execute(() =>
        {
            using var sqlConn = new SqlConnection(connBuilder.ConnectionString);
            sqlConn.Open();
            using var bc =
                new SqlBulkCopy(sqlConn, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepIdentity, null)
                {
                    DestinationTableName = tableName,
                    EnableStreaming = true,
                    BulkCopyTimeout = 360,
                    // these two need to be equal. we rely on the NotifyAfter to fire to keep the buffer clean. ideally
                    // there would be a different event on Batch completion but this will suffice.
                    BatchSize = batchSize,
                    NotifyAfter = batchSize
                };

            for (var i = 0; i < dataReader.FieldCount; i++)
            {
                var column = dataReader.GetName(i);
                if (column == "Id")
                {
                    bc.ColumnOrderHints.Add(new SqlBulkCopyColumnOrderHint("Id", SortOrder.Ascending));
                }

                bc.ColumnMappings.Add(column, column);
            }

            if (totalCopiedSoFar > 0)
            {
                // if we have already copied some data that means the bulk insert failed at some point and data has been
                // written. most of it should be commited to the database, but we are going to be in a state where we know
                // quite what was written. We've saved the last few batched of data in the buffer for replaying, but
                // we without being able to know which of that data is already persisted in the database we'll need to
                // delete all of it and then reinsert to be sure.
                //
                // Thankfully all the tables have a simple structure so we can identify the rows that have been written
                // pretty easily. All but the posttags use an Id field as their primary key so we can do a delete
                // statement for the ids in the buffer. For post tags we'll need to use the composite key.
                var keys = fileName.ToLowerInvariant() switch
                {
                    "posttags.xml" => new[] { "postid", "tag" },
                    _ => new[] { "id" }
                };

                // before we start clearing out the buffer get the count of the records in it. we'll see if the number of
                // rows in the buffer is the same as the number of rows we deleted. If that happens it means the server
                // persisted a lot more data than we can recover from so we need to fail.
                var bufferedCount = bufferedStream.GetBufferedCount();
                var conditional = string.Join(" or ", bufferedStream.GetBufferedKeys(keys));
                if (!string.IsNullOrWhiteSpace(conditional))
                {
                    var sql = $"DELETE FROM [{tableName}] WHERE {conditional}";
                    var sqlCommand = new SqlCommand(sql, sqlConn);
                    var rows = sqlCommand.ExecuteNonQuery();
                    if (rows == bufferedCount)
                    {
                        throw new InvalidOperationException(
                            "All buffered data was in the database already. No way to recover.");
                    }
                }
            }

            // because we are restarting the bulk copy process and the SqlRowsCopied sends in the number of rows it has
            // copied in its current process we need to reconcile the numbers. 
            var copiedPriorToThisBatch = totalCopiedSoFar;
            var copiedCount = 0;
            bc.SqlRowsCopied += (_, args) =>
            {
                totalCopiedSoFar = copiedPriorToThisBatch + args.RowsCopied;
                _rowsCopied(totalCopiedSoFar);
                copiedCount++;
                if (copiedCount % 3 == 0)
                {
                    // this event should fire for every two batches sent to the server. We'll only keep around the past
                    // two buffers in memory for recovery purposes and to keep memory pressure low.
                    bufferedStream?.ClearBuffer();
                }
            };

            bc.WriteToServer(bufferedStream);
            dataReader.Close();
        });
    }

    private class BufferedDataReader : IDataReader
    {
        private readonly IDataReader _innerDataReader;
        private readonly Stack<object?[]> _buffer;
        private bool _replayFromBuffer;
        private object?[]? _currentRow;

        public BufferedDataReader(IDataReader innerDataReader, int batchSize)
        {
            _innerDataReader = innerDataReader;
            _buffer = new Stack<object?[]>(batchSize);
        }

        public int GetBufferedCount() => _buffer.Count;

        public void SetReplay() => _replayFromBuffer = true;

        public void ClearBuffer() => _buffer.Clear();

        public bool Read()
        {
            // if we are replaying then grab the next row and make it the current row.
            if (_replayFromBuffer && _buffer.TryPop(out _currentRow))
            {
                return true;
            }

            // either this is already false or we just emptied the queue so set it back to false;
            _replayFromBuffer = false;
            var isRow = _innerDataReader.Read();
            if (!isRow)
            {
                // no row to read, return false;
                return false;
            }

            // we have a new row. set the current row to its fields and also store
            // its value in the replay buffer.
            var newRow = new object?[FieldCount];
            for (var i = 0; i < FieldCount; i++)
            {
                newRow[i] = _innerDataReader.IsDBNull(i)
                    ? null
                    : _innerDataReader.GetValue(i);
            }

            _buffer.Push(newRow);
            _currentRow = newRow;

            return true;
        }

        public object GetValue(int i)
        {
            return _currentRow?[i] ?? throw new InvalidOperationException("Can't read from empty row");
        }

        public bool IsDBNull(int i)
        {
            return _currentRow?[i] == null;
        }

        // delegate
        public int FieldCount => _innerDataReader.FieldCount;
        public DataTable? GetSchemaTable() => _innerDataReader.GetSchemaTable();
        public int GetOrdinal(string name) => _innerDataReader.GetOrdinal(name);
        public bool NextResult() => _innerDataReader.NextResult();
        public int Depth => _innerDataReader.Depth;
        public bool IsClosed => _innerDataReader.IsClosed;
        public int RecordsAffected => _innerDataReader.RecordsAffected;
        public Type GetFieldType(int i) => _innerDataReader.GetFieldType(i);

        // not used by SqlBulkCopy
        public bool GetBoolean(int i) => throw new NotImplementedException();
        public byte GetByte(int i) => throw new NotImplementedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();

        public char GetChar(int i) => throw new NotImplementedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) =>
            throw new NotImplementedException();

        public IDataReader GetData(int i) => throw new NotImplementedException();
        public string GetDataTypeName(int i) => throw new NotImplementedException();
        public DateTime GetDateTime(int i) => throw new NotImplementedException();
        public decimal GetDecimal(int i) => throw new NotImplementedException();
        public double GetDouble(int i) => throw new NotImplementedException();

        public float GetFloat(int i) => throw new NotImplementedException();
        public Guid GetGuid(int i) => throw new NotImplementedException();
        public short GetInt16(int i) => throw new NotImplementedException();
        public int GetInt32(int i) => throw new NotImplementedException();
        public long GetInt64(int i) => throw new NotImplementedException();
        public string GetString(int i) => throw new NotImplementedException();
        public string GetName(int i) => throw new NotImplementedException();
        public int GetValues(object[] values) => throw new NotImplementedException();
        public object this[int i] => throw new NotImplementedException();
        public object this[string name] => throw new NotImplementedException();

        public IEnumerable<string> GetBufferedKeys(IEnumerable<string> keys)
        {
            var keyAndOrdinal = keys.Select(i =>
            {
                var ordinal = GetOrdinal(i);
                return (i, ordinal, GetFieldType(ordinal));
            }).ToList();

            foreach (var row in _buffer)
            {
                var items = new List<string>();
                foreach (var (key, ordinal, dataType) in keyAndOrdinal)
                {
                    items.Add(dataType == typeof(string)
                        ? $"{key} = '{row.GetValue(ordinal)}'"
                        : $"{key} = {row.GetValue(ordinal)}");
                }

                yield return $"({string.Join(" and ", items)})";
            }
        }

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}
