using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Soddi;

#if DEBUG
Console.OutputEncoding = Encoding.UTF8;

// args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
// args = new[] { "list" };
// args = new[] { "download", "sp", "-p" };
//args = new[] { "torrent", "codegolf", "-p" };
// args = new[] { "torrent", "space,sports,travel" };
//args = new[] { "brent" };
// args = new[] { "torrent", "math" };
args = new[] { "import", @"math", "--dropAndCreate" };
// args = new[] { "import", @"travel", "--dropAndCreate", "-l", "t" };
// args = new[] { "import", @"travel", "--dropAndCreate", "--sequential" };
// args = new[] { "list", "-h" };
#endif

var container = new ServiceCollection()
    .AddSingleton<IFileSystem>(new FileSystem())
    .Scan(scan => scan.FromCallingAssembly().AddClasses());

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

