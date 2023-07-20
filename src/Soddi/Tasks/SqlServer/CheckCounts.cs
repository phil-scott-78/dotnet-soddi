using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class CheckCounts(string connectionString, Action<string, long> setResult) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress,
        CancellationToken cancellationToken)
    {
        const string TaskId = "checking-counts";
        progress.Report((TaskId, "Checking validity", 0, 1));
        var tables = new[] { "tags", "badges", "postlinks", "users", "comments", "posts", "votes", "posthistory" };

        foreach (var table in tables)
        {
            var count = await GetCountFromDbAsync(table, cancellationToken);
            setResult(table, count);
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
            await using var sqlConn = new SqlConnection(connectionString);
            await sqlConn.OpenAsync(cancellationToken);
            var command = new SqlCommand($"SELECT COUNT_BIG(*) from {tableName}", sqlConn);
            return (long)await command.ExecuteScalarAsync(cancellationToken);
        });
    }
}
