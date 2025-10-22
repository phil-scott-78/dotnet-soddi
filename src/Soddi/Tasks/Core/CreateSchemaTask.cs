using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for creating database schema
/// </summary>
public class CreateSchemaTask(
    IDatabaseProvider provider,
    ISchemaManager schemaManager,
    string connectionString,
    bool includePostTags) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("createSchema", "Creating schema", 0, GetTaskWeight()));

        using var connection = await provider.GetConnectionAsync(connectionString, cancellationToken);
        await schemaManager.CreateSchemaAsync(connection, includePostTags, cancellationToken);

        progress.Report(("createSchema", "Schema created", GetTaskWeight(), GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 10000;
    }
}
