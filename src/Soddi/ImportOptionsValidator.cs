using Soddi.Providers;
using Soddi.Services;

namespace Soddi;

/// <summary>
/// Validates ImportOptions before executing import
/// </summary>
public class ImportOptionsValidator(
    ProviderFactory providerFactory,
    IFileSystem fileSystem,
    AvailableArchiveParser availableArchiveParser)
{
    /// <summary>
    /// Validates all import options and fails fast on first error
    /// </summary>
    public async Task ValidateAsync(ImportOptions options, CancellationToken cancellationToken)
    {
        // 1. Validate path exists or can be resolved
        var resolvedPath = await ValidateAndResolvePathAsync(options.Path, cancellationToken);

        // 2. Validate provider string
        var providerType = ValidateProvider(options.Provider);

        // 3. Validate block size
        ValidateBlockSize(options.BlockSize);

        // 4. Get provider instance (validates it's available in DI)
        var provider = GetProviderInstance(providerType);

        // 5. Validate database connectivity
        var masterConnectionString = provider.GetMasterConnectionString(options.ConnectionString);
        await ValidateDatabaseConnectivityAsync(provider, masterConnectionString, cancellationToken);

        // 6. Validate database existence requirements
        var dbName = provider.GetDbNameFromPathOption(options.DatabaseName, resolvedPath);
        await ValidateDatabaseExistenceAsync(provider, masterConnectionString, dbName, options.DropAndRecreate, cancellationToken);
    }

    private async Task<string> ValidateAndResolvePathAsync(string path, CancellationToken cancellationToken)
    {
        // Check if path exists directly
        if (fileSystem.File.Exists(path) || fileSystem.Directory.Exists(path))
        {
            // If it's a file, verify it's a .7z file
            if (fileSystem.File.Exists(path) && !path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            {
                throw new SoddiException($"Invalid file format: '{path}'. Only .7z archive files are supported.");
            }

            return path;
        }

        // Try to resolve by short name from available archives
        var archives = await availableArchiveParser.Get(cancellationToken);
        var foundByShortName = archives.FirstOrDefault(i =>
            i.ShortName.Equals(path, StringComparison.InvariantCultureIgnoreCase));

        if (foundByShortName != null)
        {
            var potentialLongFileName = foundByShortName.LongName + ".7z";
            if (fileSystem.File.Exists(potentialLongFileName))
            {
                return potentialLongFileName;
            }

            throw new SoddiException(
                $"Archive '{path}' was found in available archives list as '{potentialLongFileName}', but the file does not exist locally. " +
                $"Run 'soddi download {path}' to download it first.");
        }

        throw new SoddiException(
            $"Path not found: '{path}'. " +
            $"Please specify a valid .7z file, a folder containing .7z or .xml files, or a Stack Exchange site short name.");
    }

    private DatabaseProviderType ValidateProvider(string provider)
    {
        try
        {
            return ProviderFactory.ParseProviderType(provider);
        }
        catch (ArgumentException ex)
        {
            throw new SoddiException($"Invalid provider: '{provider}'. {ex.Message}");
        }
    }

    private void ValidateBlockSize(int blockSize)
    {
        if (blockSize <= 0)
        {
            throw new SoddiException(
                $"Invalid block size: {blockSize}. Block size must be greater than 0.");
        }

        if (blockSize > 100000)
        {
            throw new SoddiException(
                $"Invalid block size: {blockSize}. Block size must be 100000 or less to avoid excessive memory usage.");
        }
    }

    private IDatabaseProvider GetProviderInstance(DatabaseProviderType providerType)
    {
        try
        {
            return providerFactory.GetProvider(providerType);
        }
        catch (Exception ex)
        {
            throw new SoddiException(
                $"Failed to initialize {providerType} provider. {ex.Message}");
        }
    }

    private async Task ValidateDatabaseConnectivityAsync(
        IDatabaseProvider provider,
        string masterConnectionString,
        CancellationToken cancellationToken)
    {
        try
        {
            // Attempt to connect to the database server
            using var connection = await provider.GetConnectionAsync(masterConnectionString, cancellationToken);

            // Connection successful - we can reach the server
        }
        catch (Exception ex)
        {
            throw new SoddiException(
                $"Failed to connect to {provider.ProviderName} database server. " +
                $"Please verify the connection string and ensure the database server is running. " +
                $"Error: {ex.Message}");
        }
    }

    private async Task ValidateDatabaseExistenceAsync(
        IDatabaseProvider provider,
        string masterConnectionString,
        string databaseName,
        bool dropAndRecreate,
        CancellationToken cancellationToken)
    {
        try
        {
            var databaseExists = await provider.DatabaseExistsAsync(masterConnectionString, databaseName, cancellationToken);

            if (!dropAndRecreate && !databaseExists)
            {
                throw new SoddiException(
                    $"Database '{databaseName}' does not exist. " +
                    $"Either create the database manually or use the --dropAndCreate flag to create it automatically.");
            }

            if (dropAndRecreate && databaseExists)
            {
                // Database will be dropped and recreated - just inform user this will happen
                // No need to throw, this is expected behavior
            }
        }
        catch (SoddiException)
        {
            // Re-throw SoddiExceptions (our validation errors)
            throw;
        }
        catch (Exception ex)
        {
            throw new SoddiException(
                $"Failed to check if database '{databaseName}' exists. " +
                $"Error: {ex.Message}");
        }
    }
}
