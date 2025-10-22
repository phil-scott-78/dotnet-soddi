using Npgsql;

namespace Soddi.Providers.Postgres;

/// <summary>
/// PostgreSQL bulk data insertion using COPY command
/// </summary>
[UsedImplicitly]
public class PostgresDataInserter : IDataInserter
{
    public async Task BulkInsertAsync(
        string connectionString,
        string tableName,
        IDataReader dataReader,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Build column list
        var columns = new List<string>();
        for (var i = 0; i < dataReader.FieldCount; i++)
        {
            columns.Add(dataReader.GetName(i).ToLowerInvariant());
        }

        var columnList = string.Join(", ", columns);

        // Use COPY for high-performance bulk insert
        // Using TEXT format (tab-delimited) for better compatibility with string-based XML data
        var copyCommand = $"COPY {tableName.ToLowerInvariant()} ({columnList}) FROM STDIN";

        long totalRows = 0;

        await using (var writer = conn.BeginTextImport(copyCommand))
        {
            try
            {
                while (dataReader.Read())
                {
                    var values = new List<string>();

                    for (var i = 0; i < dataReader.FieldCount; i++)
                    {
                        if (dataReader.IsDBNull(i))
                        {
                            // PostgreSQL uses \N for NULL in text format
                            values.Add("\\N");
                        }
                        else
                        {
                            var value = dataReader.GetValue(i);
                            var stringValue = value.ToString() ?? string.Empty;

                            // Escape special characters for COPY TEXT format
                            // Backslash, tab, newline, and carriage return need escaping
                            stringValue = stringValue
                                .Replace("\\", @"\\") // Backslash must be first
                                .Replace("\t", @"\t")
                                .Replace("\n", @"\n")
                                .Replace("\r", @"\r");

                            values.Add(stringValue);
                        }
                    }

                    // Write tab-delimited line
                    await writer.WriteLineAsync(string.Join("\t", values));
                    totalRows++;

                    // Report progress every 1000 rows
                    if (totalRows % 1000 == 0)
                    {
                        progress.Report(totalRows);
                    }
                }

                await writer.FlushAsync(cancellationToken);
                progress.Report(totalRows);
            }
            catch (Exception ex)
            {
                Log.Write(LogLevel.Error, $"Error during COPY operation for {tableName}: {ex.Message}");
                throw;
            }
        }

        dataReader.Close();
    }
}
