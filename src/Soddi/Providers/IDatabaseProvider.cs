namespace Soddi.Providers;

/// <summary>
/// Core abstraction for database operations across different providers
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>
    /// Gets the name of this provider (e.g., "SQL Server", "PostgreSQL", "Cosmos DB")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the provider type
    /// </summary>
    DatabaseProviderType ProviderType { get; }

    /// <summary>
    /// Checks if a database exists
    /// </summary>
    Task<bool> DatabaseExistsAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a database
    /// </summary>
    Task CreateDatabaseAsync(string connectionString, string databaseName, bool dropIfExists, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a connection to the specified database
    /// </summary>
    Task<IDbConnection> GetConnectionAsync(string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a connection string for the master/system database (for creating databases)
    /// </summary>
    string GetMasterConnectionString(string connectionString);

    /// <summary>
    /// Builds a connection string for a specific database
    /// </summary>
    string GetDatabaseConnectionString(string connectionString, string databaseName);

    /// <summary>
    /// Given a potentially empty database name and a path, determine the database name to use
    /// </summary>
    string GetDbNameFromPathOption(string? databaseName, string path);
}
