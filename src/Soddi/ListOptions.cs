using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Soddi.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Soddi
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class ListOptions : CommandSettings
    {
        [CommandArgument(0, "[PATTERN]")]
        [Description("Pattern to include (e.g. \"av\" includes all archives containing \"av\").")]
        public string? Pattern { get; set; }

        [CommandOption("--includeMeta")]
        [Description("Include meta databases.")]
        public bool IncludeMeta { get; set; }

        public static readonly string[][] Examples = { new[] { "list" }, new[] { "list", "spa" } };
    }

    public class ListHandler : AsyncCommand<ListOptions>
    {
        private readonly IAnsiConsole _console;
        private readonly AvailableArchiveParser _availableArchiveParser;

        public ListHandler(IAnsiConsole console, AvailableArchiveParser availableArchiveParser)
        {
            _console = console;
            _availableArchiveParser = availableArchiveParser;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, ListOptions request)
        {
            var cancellationToken = CancellationToken.None;

            var pattern = request.Pattern ?? "";
            var results = await _availableArchiveParser.Get(cancellationToken);

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

            _console.Render(table);

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
