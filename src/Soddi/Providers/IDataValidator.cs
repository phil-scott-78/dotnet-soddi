namespace Soddi.Providers;

/// <summary>
/// Validates data integrity and counts
/// </summary>
public interface IDataValidator
{
    /// <summary>
    /// Checks that row counts match expected values
    /// </summary>
    Task<Dictionary<string, (long expected, long actual)>> CheckCountsAsync(
        IDbConnection connection,
        Dictionary<string, long> expectedCounts,
        CancellationToken cancellationToken = default);
}
