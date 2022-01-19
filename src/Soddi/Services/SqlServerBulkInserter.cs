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

        using var bc =
            new SqlBulkCopy(connBuilder.ConnectionString,
                SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepIdentity)
            {
                DestinationTableName = tableName, EnableStreaming = true, NotifyAfter = 1000, BatchSize = 10000
            };

        for (var i = 0; i < dataReader.FieldCount; i++)
        {
            var column = dataReader.GetName(i);
            bc.ColumnMappings.Add(column, column);
        }

        bc.SqlRowsCopied += (_, args) =>
        {
            _rowsCopied(args.RowsCopied);
        };

        bc.WriteToServer(dataReader);
    }
}