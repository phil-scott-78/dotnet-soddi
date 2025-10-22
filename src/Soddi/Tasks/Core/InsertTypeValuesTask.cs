using Soddi.Providers;

namespace Soddi.Tasks.Core;

/// <summary>
/// Provider-agnostic task for inserting type values (lookup tables)
/// </summary>
public class InsertTypeValuesTask(
    IDatabaseProvider provider,
    ITypeValueInserter typeValueInserter,
    IFileSystem fileSystem,
    string connectionString) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("insertTypeValues", "Inserting type values", 0, GetTaskWeight()));

        using var connection = await provider.GetConnectionAsync(connectionString, cancellationToken);
        // archiveFolder is not used by the current implementations, but is part of the interface
        await typeValueInserter.InsertTypeValuesAsync(connection, fileSystem, string.Empty, cancellationToken);

        progress.Report(("insertTypeValues", "Type values inserted", GetTaskWeight(), GetTaskWeight()));
    }

    public double GetTaskWeight()
    {
        return 1000;
    }
}
