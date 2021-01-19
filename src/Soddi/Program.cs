using System;
using System.IO.Abstractions;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Soddi;
using Spectre.Cli.Extensions.DependencyInjection;
using Spectre.Console.Cli;

#if DEBUG
Console.OutputEncoding = Encoding.UTF8;
//args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
// args = new[] { "list" };
// args = new[] { "download", "sp", "-p" };
// args = new[] { "torrent", "stack", "-p" };
// args = new[] { "torrent", "iota" };
args = new[] { "import", @"sports.stackexchange.com.7z", "--dropAndCreate" };
// args = new[] { "list", "-h" };
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

#if DEBUG
            config.Settings.PropagateExceptions = true;
#endif

            config.AddCommandWithExample<ImportHandler>("import", ImportOptions.Examples);
            config.AddCommandWithExample<ListHandler>("list", ListOptions.Examples);
            config.AddCommandWithExample<DownloadHandler>("download", DownloadOptions.Examples);
            config.AddCommandWithExample<TorrentHandler>("torrent", TorrentOptions.Examples);
        });

    await app.RunAsync(args);
}
