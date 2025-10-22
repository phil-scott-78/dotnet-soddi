using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Soddi;
using Soddi.Providers;
using Soddi.Providers.SqlServer;
using Soddi.Providers.Postgres;

Console.OutputEncoding = Encoding.UTF8;

var container = new ServiceCollection()
    .AddSingleton<IFileSystem>(new FileSystem())
    .Scan(scan => scan.FromApplicationDependencies().AddClasses())
    // Register provider classes explicitly
    .AddSingleton<ProviderFactory>()
    // SQL Server providers
    .AddSingleton<SqlServerProvider>()
    .AddSingleton<SqlServerSchemaManager>()
    .AddSingleton<SqlServerDataInserter>()
    .AddSingleton<SqlServerTypeValueInserter>()
    .AddSingleton<SqlServerDataValidator>()
    // PostgreSQL providers
    .AddSingleton<PostgresProvider>()
    .AddSingleton<PostgresSchemaManager>()
    .AddSingleton<PostgresDataInserter>()
    .AddSingleton<PostgresTypeValueInserter>()
    .AddSingleton<PostgresDataValidator>();

var registrar = new TypeRegistrar(container);
var app = new CommandApp(registrar);

app.Configure(
    config =>
    {
        config.SetApplicationName("soddi");
        config.Settings.PropagateExceptions = true;
        config.SetInterceptor(new LogInterceptor());

        config.AddCommandWithExample<ImportHandler>("import", "Import a Stack Overflow archive",
            ImportOptions.Examples);
        config.AddCommandWithExample<ListHandler>("list", "List available Stack Overflow archives",
            ListOptions.Examples);
        config.AddCommandWithExample<DownloadHandler>("download", "Download a Stack Overflow archive",
            DownloadOptions.Examples);
        config.AddCommandWithExample<BrentHandler>("brent",
            "Download one of the available SQL Server databases hosted by Brent Ozar", BrentOptions.Examples, true);
        config.AddCommandWithExample<TorrentHandler>("torrent", "Download Stack Overflow archive via BitTorrent",
            TorrentOptions.Examples);
    });


try
{
    await app.RunAsync(args);
}
catch (Exception e)
{
    AnsiConsole.WriteException(e);
    return -1;
}

return 0;
