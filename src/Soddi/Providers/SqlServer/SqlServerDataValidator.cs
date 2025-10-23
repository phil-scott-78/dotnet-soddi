using Microsoft.Data.SqlClient;

namespace Soddi.Providers.SqlServer;

/// <summary>
/// SQL Server data validation implementation
/// </summary>
[UsedImplicitly]
public class SqlServerDataValidator : IDataValidator
{
    public async Task<Dictionary<string, (long expected, long actual)>> CheckCountsAsync(
        IDbConnection connection,
        Dictionary<string, long> expectedCounts,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, (long expected, long actual)>();

        foreach (var (tableName, expectedCount) in expectedCounts)
        {
            var actualCount = await GetCountFromDbAsync(connection, tableName, cancellationToken);
            results[tableName] = (expectedCount, actualCount);
        }

        return results;
    }

    private async Task<long> GetCountFromDbAsync(IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        return await SqlServerRetryPolicy.Policy.ExecuteAsync(async () =>
        {
            await using var command = new SqlCommand($"SELECT COUNT_BIG(*) FROM {tableName}", (SqlConnection)connection);
            return (long)await command.ExecuteScalarAsync(cancellationToken);
        });
    }
}
