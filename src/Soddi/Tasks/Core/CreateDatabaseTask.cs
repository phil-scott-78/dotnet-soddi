using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for creating a database
/// </summary>
public class CreateDatabaseTask(IDatabaseProvider provider, string connectionString, string databaseName) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("createDb", "Creating database", GetTaskWeight() / 2, GetTaskWeight()));
        await provider.CreateDatabaseAsync(connectionString, databaseName, dropIfExists: true, cancellationToken);
        progress.Report(("createDb", "Database created", GetTaskWeight() / 2, GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 10000;
    }
}
