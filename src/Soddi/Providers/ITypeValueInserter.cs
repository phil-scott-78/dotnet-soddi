namespace Soddi.Providers;

/// <summary>
/// Handles insertion of type/lookup table values
/// </summary>
public interface ITypeValueInserter
{
    /// <summary>
    /// Inserts type values (LinkTypes, PostHistoryTypes, PostTypes, VoteTypes)
    /// </summary>
    Task InsertTypeValuesAsync(IDbConnection connection, IFileSystem fileSystem, string archiveFolder, CancellationToken cancellationToken = default);
}
