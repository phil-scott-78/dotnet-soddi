using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Soddi;
using Spectre.Cli.Extensions.DependencyInjection;
using Spectre.Console.Cli;

#if DEBUG
//args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
// args = new[] { "list" };
// args = new[] { "download", "sp", "-p" };
// args = new[] { "torrent", "stack", "-p" };
// args = new[] { "download", "iota" };
// args = new[] { "import", @"iota.stackexchange.com.7z", "--dropAndCreate" };
args = new[] { "-h" };
#endif

{
    var container = new ServiceCollection()
        .AddSingleton<IFileSystem>(new FileSystem())
        .Scan(scan => scan.FromCallingAssembly().AddClasses());

    using var registrar = new DependencyInjectionRegistrar(container);
    var app = new CommandApp(registrar);

    app.Configure(
        config =>
        {
            config.SetApplicationName("soddi");
            config.AddCommand<ImportHandler>("import")
                .WithDescription("Import an archive into SQL Server");
            config.AddCommand<ListHandler>("list")
                .WithDescription("List available archives");
            config.AddCommand<DownloadHandler>("download")
                .WithDescription("Download an archive");
            config.AddCommand<TorrentHandler>("torrent")
                .WithDescription("Download an archive via bittorrent");
        });

    await app.RunAsync(args);
}
