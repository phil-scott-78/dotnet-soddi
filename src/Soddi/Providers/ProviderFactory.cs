namespace Soddi.Providers;

/// <summary>
/// Factory for creating database provider instances based on provider type
/// </summary>
public class ProviderFactory(IServiceProvider serviceProvider)
{
    /// <summary>
    /// Gets a database provider by type
    /// </summary>
    public IDatabaseProvider GetProvider(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.SqlServer => GetService<SqlServer.SqlServerProvider>(),
            DatabaseProviderType.Postgres => GetService<Postgres.PostgresProvider>(),
            DatabaseProviderType.Cosmos => throw new NotImplementedException("Cosmos DB provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    /// <summary>
    /// Gets a schema manager for the specified provider type
    /// </summary>
    public ISchemaManager GetSchemaManager(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.SqlServer => GetService<SqlServer.SqlServerSchemaManager>(),
            DatabaseProviderType.Postgres => GetService<Postgres.PostgresSchemaManager>(),
            DatabaseProviderType.Cosmos => throw new NotImplementedException("Cosmos DB provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    /// <summary>
    /// Gets a data inserter for the specified provider type
    /// </summary>
    public IDataInserter GetDataInserter(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.SqlServer => GetService<SqlServer.SqlServerDataInserter>(),
            DatabaseProviderType.Postgres => GetService<Postgres.PostgresDataInserter>(),
            DatabaseProviderType.Cosmos => throw new NotImplementedException("Cosmos DB provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    /// <summary>
    /// Gets a type value inserter for the specified provider type
    /// </summary>
    public ITypeValueInserter GetTypeValueInserter(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.SqlServer => GetService<SqlServer.SqlServerTypeValueInserter>(),
            DatabaseProviderType.Postgres => GetService<Postgres.PostgresTypeValueInserter>(),
            DatabaseProviderType.Cosmos => throw new NotImplementedException("Cosmos DB provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    /// <summary>
    /// Gets a data validator for the specified provider type
    /// </summary>
    public IDataValidator GetDataValidator(DatabaseProviderType providerType)
    {
        return providerType switch
        {
            DatabaseProviderType.SqlServer => GetService<SqlServer.SqlServerDataValidator>(),
            DatabaseProviderType.Postgres => GetService<Postgres.PostgresDataValidator>(),
            DatabaseProviderType.Cosmos => throw new NotImplementedException("Cosmos DB provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown provider type: {providerType}", nameof(providerType))
        };
    }

    /// <summary>
    /// Parses a provider string to DatabaseProviderType
    /// </summary>
    public static DatabaseProviderType ParseProviderType(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "sqlserver" or "sql" or "mssql" => DatabaseProviderType.SqlServer,
            "postgres" or "postgresql" or "pg" => DatabaseProviderType.Postgres,
            "cosmos" or "cosmosdb" => DatabaseProviderType.Cosmos,
            _ => throw new ArgumentException($"Unknown provider: {provider}. Valid values are: sqlserver, postgres, cosmos", nameof(provider))
        };
    }

    private T GetService<T>() where T : class
    {
        if (serviceProvider.GetService(typeof(T)) is not T service)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} not registered in DI container");
        }

        return service;
    }
}
