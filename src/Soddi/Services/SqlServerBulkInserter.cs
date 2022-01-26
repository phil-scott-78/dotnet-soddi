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

        var bc =
            new SqlBulkCopy(connBuilder.ConnectionString,
                SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepIdentity)
            {
                DestinationTableName = tableName, EnableStreaming = true, NotifyAfter = 1000, BatchSize = 50000
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

        bc.SqlRowsCopied += (_, args) =>
        {
            _rowsCopied(args.RowsCopied);
        };

        var failureCount = 0;
        var p = Policy
            .Handle<SqlException>()
            .WaitAndRetry(
                100,
                retryAttempt => TimeSpan.FromMilliseconds(Math.Min(retryAttempt * 100, 100)),
                (exception, span) =>
                {
                    bc.BatchSize = (int)(bc.BatchSize * .5);
                    failureCount++;
                    if (failureCount > 10)
                    {
                        AnsiConsole.MarkupLine($"[bold]{fileName}[/][red]{exception.Message}[/]{Environment.NewLine}Retrying in [blue]{span.Humanize()}[/] with a batch size of [cyan]{bc.BatchSize}[/].");
                        failureCount = 0;
                    }
                });

        p.Execute(() =>
        {
            bc.WriteToServer(dataReader);
            ((IDisposable)bc).Dispose();
        });
    }
}
