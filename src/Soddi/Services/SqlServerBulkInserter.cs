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
        var batchSize = fileName.ToLowerInvariant() switch
        {
            "posts.xml" => 500,
            "posthistory.xml" => 500,
            "comments.xml" => 2000,
            _ => 10_000
        };

        var bufferedStream = new BufferedDataReader(dataReader, batchSize);

        var retryPolicy = Policy
            .Handle<SqlException>()
            .WaitAndRetryForever(_ => TimeSpan.FromMilliseconds(500), (_, _, _) =>
                {
                    bufferedStream.Replay();
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
                    NotifyAfter = batchSize,
                    BulkCopyTimeout = 360
                };

            bc.BatchSize = batchSize;
            bc.NotifyAfter = batchSize;

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
                var keys = fileName.ToLowerInvariant() switch
                {
                    "posttags.xml" => new[] { "postid", "tag" },
                    _ => new[] { "id" }
                };

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

            var copiedPriorToThisBatch = totalCopiedSoFar;
            var copiedCount = 0;
            bc.SqlRowsCopied += (_, args) =>
            {
                totalCopiedSoFar = copiedPriorToThisBatch + args.RowsCopied;
                _rowsCopied(totalCopiedSoFar);
                copiedCount++;
                if (copiedCount % 3 == 0)
                {
                    bufferedStream?.Clear();
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

        public int GetBufferedCount()
        {
            return _buffer.Count;
        }

        public void Replay()
        {
            _replayFromBuffer = true;
        }

        public void Clear()
        {
            _buffer.Clear();
        }

        public void Close()
        {
        }

        public void Dispose()
        {
        }

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

        public int GetOrdinal(string name)
        {
            return _innerDataReader.GetOrdinal(name);
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
    }
}
