namespace Soddi.Providers;

/// <summary>
/// Manages database schema operations (DDL)
/// </summary>
public interface ISchemaManager
{
    /// <summary>
    /// Creates all tables and schema objects
    /// </summary>
    Task CreateSchemaAsync(IDbConnection connection, bool includePostTags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds primary key and unique constraints
    /// </summary>
    Task AddConstraintsAsync(IDbConnection connection, bool skipConstraints, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds foreign key constraints
    /// </summary>
    Task AddForeignKeysAsync(IDbConnection connection, CancellationToken cancellationToken = default);
}
