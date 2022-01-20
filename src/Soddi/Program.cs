using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Soddi;

#if DEBUG
Console.OutputEncoding = Encoding.UTF8;
const bool propagateExceptions = true;

// args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
// args = new[] { "list" };
// args = new[] { "download", "sp", "-p" };
//args = new[] { "torrent", "codegolf", "-p" };
args = new[] { "torrent", "space,sports,travel" };
//args = new[] { "brent" };
//args = new[] { "torrent", "sports" };
// args = new[] { "import", @"sports.stackexchange.com.7z", "--dropAndCreate" };
// args = new[] { "list", "-h" };
#else
const bool propagateExceptions = false;
#endif

{
    var container = new ServiceCollection()
        .AddSingleton<IFileSystem>(new FileSystem())
        .Scan(scan => scan.FromCallingAssembly().AddClasses());

    var registrar = new TypeRegistrar(container);
    var app = new CommandApp(registrar);

    app.Configure(
        config =>
        {
            config.SetApplicationName("soddi");
            config.Settings.PropagateExceptions = propagateExceptions;

            config.AddCommandWithExample<ImportHandler>("import", "Import a Stack Overflow archive", ImportOptions.Examples);
            config.AddCommandWithExample<ListHandler>("list", "List available Stack Overflow archives", ListOptions.Examples);
            config.AddCommandWithExample<DownloadHandler>("download", "Download a Stack Overflow archive", DownloadOptions.Examples);
            config.AddCommandWithExample<BrentHandler>("brent", "Download one of the available SQL Server databases hosted by Brent Ozar", BrentOptions.Examples, true);
            config.AddCommandWithExample<TorrentHandler>("torrent", "Download Stack Overflow archive via BitTorrent", TorrentOptions.Examples);
        });

    await app.RunAsync(args);
}
