using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using Soddi.Services;
using Spectre.Console;

namespace Soddi
{
    [Verb("list", HelpText = "List available Stack Overflow data dumps."), UsedImplicitly]
    public class ListOptions : IRequest<int>
    {
        public ListOptions(string pattern, bool includeMeta)
        {
            Pattern = pattern;
            IncludeMeta = includeMeta;
        }

        [Value(0, MetaName = "Pattern", Required = false, Default = "",
            HelpText = "Pattern to include (e.g. \"av\" includes all archives containing \"av\").")]
        public string Pattern { get; }

        [Option("includeMeta",
            HelpText = "Include meta databases.",
            Required = false,
            Default = false)]
        public bool IncludeMeta { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("List all archives",
                    new ListOptions("", false));
                yield return new Example("List all archives containing the letters \"av\"",
                    new ListOptions("av", false));
                yield return new Example("List all archives containing the letters \"av\" including meta sites",
                    new ListOptions("av", true));
            }
        }
    }

    public class ListHandler : IRequestHandler<ListOptions, int>
    {
        public async Task<int> Handle(ListOptions request, CancellationToken cancellationToken)
        {
            var pattern = request.Pattern;
            var parser = new AvailableArchiveParser();
            var results = await parser.Get(cancellationToken);

            var table = new Table { Border = TableBorder.Rounded, Expand = true };
            table.AddColumn(new TableColumn("Short Name"));
            table.AddColumn(new TableColumn("Archive"));

            var filteredResults = results.Where(i => i.ShortName.Contains(pattern));
            if (request.IncludeMeta == false)
            {
                filteredResults = filteredResults.Where(i => !i.LongName.Contains(".meta."));
            }

            foreach (var archive in filteredResults)
            {
                var innerTable = new Table { Border = TableBorder.None, ShowHeaders = false, Expand = true };

                innerTable.AddColumn("Uri");
                innerTable.AddColumn("Size", column =>
                {
                    column.Alignment = Justify.Right;
                    column.Width = 7;
                });

                foreach (var archiveUri in archive.Uris)
                {
                    innerTable.AddRow(new Markup($"[link={archiveUri.Uri}]{archiveUri.Uri}[/]"),
                        new Text(archiveUri.SizeInBytes.BytesToString()));
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

        public static string KiloBytesToString(this double kbCount)
        {
            return ((long)kbCount * 1024).BytesToString();
        }

        public static string BytesToString(this long byteCount)
        {
            // from https://stackoverflow.com/a/4975942
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];

            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString(CultureInfo.InvariantCulture) + suf[place];
        }
    }
}
