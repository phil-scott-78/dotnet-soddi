using Npgsql;

namespace Soddi.Providers.Postgres;

/// <summary>
/// PostgreSQL data validation implementation
/// </summary>
[UsedImplicitly]
public class PostgresDataValidator : IDataValidator
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
        // PostgreSQL doesn't have COUNT_BIG, just use COUNT(*)
        // It returns bigint (int64) by default for COUNT(*)
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {tableName.ToLowerInvariant()}", (NpgsqlConnection)connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null ? (long)result : 0;
    }
}
