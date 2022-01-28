using Microsoft.Data.SqlClient;

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

        using var bc = new SqlBulkCopy(connBuilder.ConnectionString, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepIdentity)
        {
            DestinationTableName = tableName, EnableStreaming = true, NotifyAfter = 1_000, BulkCopyTimeout = 360
        };

        var batchSize = fileName.ToLowerInvariant() switch
        {
            "posts.xml" => 100,
            "posthistory.xml" => 100,
            "comments.xml" => 1000,
            _ => 10_000
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

        bc.SqlRowsCopied += (_, args) =>
        {
            _rowsCopied(args.RowsCopied);
        };

        bc.WriteToServer(dataReader);
    }
}
