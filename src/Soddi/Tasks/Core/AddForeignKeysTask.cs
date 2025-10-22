using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for adding foreign keys
/// </summary>
public class AddForeignKeysTask(
    IDatabaseProvider provider,
    ISchemaManager schemaManager,
    string connectionString) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("addForeignKeys", "Adding foreign keys", 0, GetTaskWeight()));

        using var connection = await provider.GetConnectionAsync(connectionString, cancellationToken);
        await schemaManager.AddForeignKeysAsync(connection, cancellationToken);

        progress.Report(("addForeignKeys", "Foreign keys added", GetTaskWeight(), GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 50000;
    }
}
