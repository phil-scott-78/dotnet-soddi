using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for verifying a database exists
/// </summary>
public class VerifyDatabaseExistsTask(IDatabaseProvider provider, string connectionString, string databaseName) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("verifyDb", "Verifying database exists", GetTaskWeight() / 2, GetTaskWeight()));

        var exists = await provider.DatabaseExistsAsync(connectionString, databaseName, cancellationToken);

        if (!exists)
        {
            throw new SoddiException(
                $"Database {databaseName} does not exist.\nDatabase must exist, or use the --dropAndCreate option to build a default database.");
        }

        progress.Report(("verifyDb", "Database verified", GetTaskWeight() / 2, GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 100;
    }
}
