using Npgsql;

namespace Soddi.Providers.Postgres;

/// <summary>
/// PostgreSQL database provider implementation
/// </summary>
[UsedImplicitly]
public class PostgresProvider : IDatabaseProvider
{
    private readonly IFileSystem _fileSystem;

    public PostgresProvider(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ProviderName => "PostgreSQL";

    public DatabaseProviderType ProviderType => DatabaseProviderType.Postgres;

    public async Task<bool> DatabaseExistsAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM pg_database WHERE datname = '{databaseName}'";
        await using var conn = new NpgsqlConnection(connectionString);
        await using var command = new NpgsqlCommand(sql, conn);

        await conn.OpenAsync(cancellationToken);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && (long)result > 0;
    }

    public async Task CreateDatabaseAsync(string connectionString, string databaseName, bool dropIfExists, CancellationToken cancellationToken = default)
    {
        if (!dropIfExists)
        {
            throw new InvalidOperationException("Database creation requires dropIfExists to be true");
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Terminate existing connections
        var terminateSql = $@"
SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = '{databaseName}'
  AND pid <> pg_backend_pid();";

        await using (var terminateCmd = new NpgsqlCommand(terminateSql, conn))
        {
            await terminateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Drop database if exists
        var dropSql = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await using (var dropCmd = new NpgsqlCommand(dropSql, conn))
        {
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create database
        var createSql = $"CREATE DATABASE \"{databaseName}\" ENCODING = 'UTF8'";
        await using (var createCmd = new NpgsqlCommand(createSql, conn))
        {
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IDbConnection> GetConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public string GetMasterConnectionString(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = "postgres" // Connect to default postgres database
            };
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            throw new SoddiException("Could not parse connection string");
        }
    }

    public string GetDatabaseConnectionString(string connectionString, string databaseName)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = databaseName
            };
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            throw new SoddiException("Could not parse connection string");
        }
    }

    /// <summary>
    /// Given a potentially empty database name and a path figure out what to call a database
    /// </summary>
    public string GetDbNameFromPathOption(string? databaseName, string path)
    {
        if (!string.IsNullOrWhiteSpace(databaseName))
        {
            return databaseName;
        }

        if (_fileSystem.Directory.Exists(path))
        {
            return _fileSystem.DirectoryInfo.New(path).Name;
        }

        if (_fileSystem.File.Exists(path))
        {
            return _fileSystem.Path.GetFileNameWithoutExtension(path);
        }

        throw new FileNotFoundException("Database archive path not found", path);
    }
}
