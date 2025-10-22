using Microsoft.Data.SqlClient;

namespace Soddi.Providers.SqlServer;

/// <summary>
/// SQL Server database provider implementation
/// </summary>
[UsedImplicitly]
public class SqlServerProvider : IDatabaseProvider
{
    private readonly IFileSystem _fileSystem;

    public SqlServerProvider(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ProviderName => "SQL Server";

    public DatabaseProviderType ProviderType => DatabaseProviderType.SqlServer;

    public async Task<bool> DatabaseExistsAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT COUNT(*) FROM sys.databases WHERE name = '{databaseName}'";
        await using var sqlConn = new SqlConnection(connectionString);
        await using var sqlCommand = new SqlCommand(sql, sqlConn);

        await sqlConn.OpenAsync(cancellationToken);
        var result = (int)await sqlCommand.ExecuteScalarAsync(cancellationToken);
        return result > 0;
    }

    public async Task CreateDatabaseAsync(string connectionString, string databaseName, bool dropIfExists, CancellationToken cancellationToken = default)
    {
        if (!dropIfExists)
        {
            throw new InvalidOperationException("Database creation requires dropIfExists to be true");
        }

        var sql = DatabaseCreationSql.Replace("DummyDatabaseName", databaseName);
        var statements = sql.Split("GO");

        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(cancellationToken);

        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;

            await using var command = new SqlCommand(statement, sqlConn);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task<IDbConnection> GetConnectionAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public string GetMasterConnectionString(string connectionString)
    {
        try
        {
            return new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master"
            }.ConnectionString;
        }
        catch (ArgumentException e) when (e.Message.StartsWith(
                                              "Format of the initialization string does not conform to specification starting",
                                              StringComparison.InvariantCultureIgnoreCase))
        {
            throw new SoddiException("Could not parse connection string");
        }
    }

    public string GetDatabaseConnectionString(string connectionString, string databaseName)
    {
        try
        {
            return new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;
        }
        catch (ArgumentException e) when (e.Message.StartsWith(
                                              "Format of the initialization string does not conform to specification starting",
                                              StringComparison.InvariantCultureIgnoreCase))
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

    private const string DatabaseCreationSql = @"
USE [master]
GO

IF EXISTS(select NULL from sys.databases where name = 'DummyDatabaseName' )
    ALTER DATABASE [DummyDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
GO

IF EXISTS(select NULL from sys.databases where name = 'DummyDatabaseName' )
    DROP DATABASE [DummyDatabaseName]
GO

CREATE DATABASE [DummyDatabaseName]
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_NULL_DEFAULT OFF
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_NULLS OFF
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_PADDING OFF
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_WARNINGS OFF
GO

ALTER DATABASE [DummyDatabaseName] SET ARITHABORT OFF
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_CLOSE OFF
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_SHRINK OFF
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_UPDATE_STATISTICS ON
GO

ALTER DATABASE [DummyDatabaseName] SET CURSOR_CLOSE_ON_COMMIT OFF
GO

ALTER DATABASE [DummyDatabaseName] SET CURSOR_DEFAULT  GLOBAL
GO

ALTER DATABASE [DummyDatabaseName] SET CONCAT_NULL_YIELDS_NULL OFF
GO

ALTER DATABASE [DummyDatabaseName] SET NUMERIC_ROUNDABORT OFF
GO

ALTER DATABASE [DummyDatabaseName] SET QUOTED_IDENTIFIER OFF
GO

ALTER DATABASE [DummyDatabaseName] SET RECURSIVE_TRIGGERS OFF
GO

ALTER DATABASE [DummyDatabaseName] SET DISABLE_BROKER
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_UPDATE_STATISTICS_ASYNC OFF
GO

ALTER DATABASE [DummyDatabaseName] SET DATE_CORRELATION_OPTIMIZATION OFF
GO

ALTER DATABASE [DummyDatabaseName] SET TRUSTWORTHY OFF
GO

ALTER DATABASE [DummyDatabaseName] SET ALLOW_SNAPSHOT_ISOLATION OFF
GO

ALTER DATABASE [DummyDatabaseName] SET PARAMETERIZATION SIMPLE
GO

ALTER DATABASE [DummyDatabaseName] SET READ_COMMITTED_SNAPSHOT OFF
GO

ALTER DATABASE [DummyDatabaseName] SET HONOR_BROKER_PRIORITY OFF
GO

ALTER DATABASE [DummyDatabaseName] SET RECOVERY SIMPLE
GO

ALTER DATABASE [DummyDatabaseName] SET  MULTI_USER
GO

ALTER DATABASE [DummyDatabaseName] SET PAGE_VERIFY CHECKSUM
GO

ALTER DATABASE [DummyDatabaseName] SET DB_CHAINING OFF
GO

ALTER DATABASE [DummyDatabaseName] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF )
GO

ALTER DATABASE [DummyDatabaseName] SET TARGET_RECOVERY_TIME = 60 SECONDS
GO

ALTER DATABASE [DummyDatabaseName] SET DELAYED_DURABILITY = DISABLED
GO

ALTER DATABASE [DummyDatabaseName] SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE)
GO

ALTER DATABASE [DummyDatabaseName] SET  READ_WRITE
GO
";
}
