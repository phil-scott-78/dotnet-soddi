using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace Soddi.Services
{
    public class SqlServerBulkInserter
    {
        private readonly string _connectionString;
        private readonly string _dbName;
        private readonly Action<long> _rowsCopied;

        public SqlServerBulkInserter(string connectionString, string dbName, Action<long> rowsCopied)
        {
            _connectionString = connectionString;
            _dbName = dbName;
            _rowsCopied = rowsCopied;
        }

        public void Insert(IDataReader dataReader, string fileName)
        {
            var tableName = Path.GetFileNameWithoutExtension(fileName);

            var connBuilder = new SqlConnectionStringBuilder(_connectionString) {InitialCatalog = _dbName};

            using var bc =
                new SqlBulkCopy(connBuilder.ConnectionString,
                    SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.KeepIdentity)
                {
                    DestinationTableName = tableName,
                    EnableStreaming = true,
                    NotifyAfter = 500,
                    BatchSize = 5000
                };

            for (var i = 0; i < dataReader.FieldCount; i++)
            {
                var column = dataReader.GetDataTypeName(i);
                bc.ColumnMappings.Add(column, column);
            }

            bc.SqlRowsCopied += (sender, args) =>
            {
                _rowsCopied(args.RowsCopied);
            };

            bc.WriteToServer(dataReader);
        }
    }
}
