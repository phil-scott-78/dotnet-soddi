using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using MediatR;
using Spectre.Console;

namespace Soddi
{
    [Verb("list", HelpText = "List available database archives.")]
    public class ListOptions : IRequest<int>
    {
        [Option('p', "pattern", HelpText = "Pattern to include")]
        public string Pattern { get; set; } = string.Empty;
    }

    public class ListHandler : IRequestHandler<ListOptions, int>
    {
        public async Task<int> Handle(ListOptions request, CancellationToken cancellationToken)
        {
            var parser = new AvailableArchiveParser();
            var results = await parser.Get(cancellationToken);

            var table = new Table {Border = Border.Rounded};
            table.AddColumn(new TableColumn("Short Name"));
            table.AddColumn(new TableColumn("Archive"));


            foreach (var archive in results.Where(i => i.ShortName.Contains(request.Pattern)))
            {
                var innerTable = new Table {Border = Border.None, ShowHeaders = false, Expand = true};

                innerTable.AddColumn("Uri");
                innerTable.AddColumn("Size", column =>
                {
                    column.Alignment = Justify.Right;
                    column.Width = 7;
                });

                foreach (var archiveUri in archive.Uris)
                {
                    innerTable.AddRow(new Markup($"[link={archiveUri.Uri}]{archiveUri.Uri}[/]"), new Text(archiveUri.SizeInBytes.BytesToString()));
                }

                table.AddRow(new Markup($"[white]{archive.ShortName}[/]"), innerTable);
            }

            AnsiConsole.Render(table);

            return await Task.FromResult(0);
        }
    }

    public static class FileSizeHelper
    {
        public static string KiloBytesToString(this int kiloByteCount)
        {
            return BytesToString(kiloByteCount * 1024L);
        }

        public static string BytesToString(this long byteCount)
        {
            // from https://stackoverflow.com/a/4975942
            string[] suf = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString(CultureInfo.InvariantCulture) + suf[place];
        }
    }
}
