namespace Soddi.Providers;

/// <summary>
/// Handles bulk data insertion operations
/// </summary>
public interface IDataInserter
{
    /// <summary>
    /// Bulk inserts data from an IDataReader into the specified table
    /// </summary>
    Task BulkInsertAsync(
        string connectionString,
        string tableName,
        IDataReader dataReader,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);
}
