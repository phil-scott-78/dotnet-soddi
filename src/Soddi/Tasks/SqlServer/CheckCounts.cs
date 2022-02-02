using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class CheckCounts : ITask
{
    private readonly string _connectionString;
    private readonly Action<string, long> _setResult;

    public CheckCounts(string connectionString, Action<string, long> setResult)
    {
        _connectionString = connectionString;
        _setResult = setResult;
    }

    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress,
        CancellationToken cancellationToken)
    {
        const string TaskId = "checking-counts";
        progress.Report((TaskId, "Checking validity", 0, 1));
        var tables = new[] { "tags", "badges", "postlinks", "users", "comments", "posts", "votes", "posthistory" };

        foreach (var table in tables)
        {
            var count = await GetCountFromDbAsync(table, cancellationToken);
            _setResult(table, count);
        }
    }

    public double GetTaskWeight()
    {
        return 100;
    }

    private async Task<long> GetCountFromDbAsync(string tableName, CancellationToken cancellationToken)
    {
        return await RetryPolicy.Policy.ExecuteAsync(async () =>
        {
            await using var sqlConn = new SqlConnection(_connectionString);
            await sqlConn.OpenAsync(cancellationToken);
            var command = new SqlCommand($"SELECT COUNT_BIG(*) from {tableName}", sqlConn);
            return (long)await command.ExecuteScalarAsync(cancellationToken);
        });
    }
}
