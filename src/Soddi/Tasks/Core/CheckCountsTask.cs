using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for validating row counts
/// </summary>
public class CheckCountsTask(
    IDatabaseProvider provider,
    IDataValidator dataValidator,
    string connectionString,
    Action<string, long> setResult) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        const string TaskId = "checking-counts";
        progress.Report((TaskId, "Checking validity", 0, 1));

        var tables = new[] { "tags", "badges", "postlinks", "users", "comments", "posts", "votes", "posthistory" };
        var expectedCounts = new Dictionary<string, long>();

        // We don't have expected counts, so we'll just use 0 as a placeholder
        // The validator will return actual counts which we'll report via the callback
        foreach (var table in tables)
        {
            expectedCounts[table] = 0;
        }

        using var connection = await provider.GetConnectionAsync(connectionString, cancellationToken);
        var results = await dataValidator.CheckCountsAsync(connection, expectedCounts, cancellationToken);

        foreach (var (tableName, (_, actual)) in results)
        {
            setResult(tableName, actual);
        }
    }

    public double GetTaskWeight()
    {
        return 100;
    }
}
