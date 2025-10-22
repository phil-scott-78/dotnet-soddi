using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for adding database constraints
/// </summary>
public class AddConstraintsTask(
    IDatabaseProvider provider,
    ISchemaManager schemaManager,
    string connectionString,
    bool skipConstraints) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("addConstraints", "Adding constraints", 0, GetTaskWeight()));

        using var connection = await provider.GetConnectionAsync(connectionString, cancellationToken);
        await schemaManager.AddConstraintsAsync(connection, skipConstraints, cancellationToken);

        progress.Report(("addConstraints", "Constraints added", GetTaskWeight(), GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 10000;
    }
}
