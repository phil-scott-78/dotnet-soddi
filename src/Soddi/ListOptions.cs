using Soddi.Services;

namespace Soddi;

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
        var cancellationToken = new CancellationToken();

        var pattern = request.Pattern ?? "";
        var results = await _availableArchiveParser.Get(cancellationToken);

        var table = new Table().NoBorder().Expand().HideHeaders();
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
                var filename = archiveUri.Uri.AbsolutePath.Replace("/download/stackexchange/", "");
                innerTable.AddRow(new Markup($"[link={archiveUri.Uri}]{filename}[/]"),
                    new Text(archiveUri.SizeInBytes.BytesToString()));
            }

            table.AddRow(new Markup($"[white]{archive.ShortName}[/]"), innerTable);
        }

        _console.Write(table);

        return await Task.FromResult(0);
    }
}

public static class FileSizeHelper
{
    public static string BytesToString(this long byteCount)
    {
        return byteCount.Bytes().ToString();
    }
}
