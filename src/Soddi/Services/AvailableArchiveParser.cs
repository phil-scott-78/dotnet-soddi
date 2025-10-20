﻿using System.Xml.Linq;

namespace Soddi.Services;

public class Archive(string shortName, string longName, List<Archive.UriWithSize> uris)
{
    public string ShortName { get; } = shortName;
    public string LongName { get; } = longName;
    public List<UriWithSize> Uris { get; } = uris;

    public class UriWithSize(Uri uri, long sizeInBytes, IFileSystem? fileSystem = null)
    {
        private readonly IFileSystem _fileSystem = fileSystem ?? new FileSystem();

        public Uri Uri { get; } = uri;
        public long SizeInBytes { get; } = sizeInBytes;

        public string Description(bool includeFileSize = true)
        {
            var fileName = _fileSystem.Path.GetFileName(Uri.AbsolutePath);
            return includeFileSize ? $"{fileName} ({SizeInBytes.BytesToString()})" : fileName;
        }
    }
}

public class AvailableArchiveParser(IAnsiConsole console)
{
    public async Task<IEnumerable<Archive>> Get(CancellationToken cancellationToken)
    {
        const string BaseUrl = "https://archive.org/download/stackexchange/";
        const string DownloadUrl = BaseUrl + "stackexchange_files.xml";

        var client = new HttpClient();
        using var response = await client
            .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        if (doc.Root?.Document == null)
        {
            throw new Exception("Could not parse stackexchange_files.xml. XML Document was null");
        }

        // we need to find all the items that point to a .7z file and pull out
        // their size
        var items = doc.Root.Elements()
            .Where(i => i.Element("format")?.Value == "7z")
            .Select(i => new
            {
                Name = i.Attribute("name")?.Value ?? throw new Exception("Name not found"),
                Size = i.Element("size")?.Value
            })
            .ToList();

        // because stackoverflow is split into multiple files we need to group all these .7z files
        // by everything to the left of a dash (if it exists). this way stackoverflow.com-posts,
        // stackoverflow.com-comments all are grouped together while math.7z gets put into a grouping of one
        var names = items.Select(i => StripDashName(i.Name)).Distinct().ToArray();

        return names
            .Select(archive => new
            {
                name = archive,
                uris = items
                    .Where(i => StripDashName(i.Name) == archive)
                    .Select(i => new Archive.UriWithSize(new Uri(BaseUrl + i.Name), long.Parse(i.Size ?? "0")))
                    .ToList()
            })
            .Select(archive => new Archive(
                shortName: archive.name.Replace(".stackexchange.com.7z", ""),
                longName: archive.name.Replace(".7z", ""),
                archive.uris));
    }

    public async Task<List<Archive>> FindOrPickArchive(string archiveItems, bool canUserPick,
        CancellationToken cancellationToken)
    {
        var archives = archiveItems.Split(',', ';');
        var results = (await this.Get(cancellationToken)).ToList();

        var archivesToDownload = new List<Archive>();
        foreach (var archive in archives)
        {
            var archiveUrl = results
                .FirstOrDefault(i => i.ShortName == archive ||
                                     i.LongName == archive ||
                                     i.ShortName.Contains($"{archive}-"));

            if (archiveUrl != null)
            {
                archivesToDownload.Add(archiveUrl);
            }
        }

        if (archivesToDownload.Count > 0)
            return archivesToDownload;

        if (!canUserPick)
        {
            throw new SoddiException($"Could not find archive named {archiveItems}");
        }

        var filteredResults = results.Where(i =>
            i.ShortName.Contains(archiveItems, StringComparison.InvariantCultureIgnoreCase)).ToList();

        if (filteredResults.Count == 0)
        {
            throw new SoddiException($"Could not find archive named {archiveItems}");
        }

        var item = console.Prompt(
            new SelectionPrompt<ArchiveSelectionOption>()
                .PageSize(10)
                .Title("Pick an archive to download")
                .AddChoices(filteredResults
                    .Where(i => !i.ShortName.Contains("meta.", StringComparison.InvariantCultureIgnoreCase))
                    .Select(ArchiveSelectionOption.FromArchive)));

        return results
            .Where(i => i.ShortName.Equals(item.ShortName, StringComparison.InvariantCultureIgnoreCase))
            .ToList();
    }


    private static string StripDashName(string input)
    {
        var index = input.IndexOf("-", StringComparison.Ordinal);
        return index < 0 ? input : input[..index];
    }
}

internal class ArchiveSelectionOption
{
    private readonly string _title;

    private ArchiveSelectionOption(string shortName, string title)
    {
        ShortName = shortName;
        _title = title;
    }

    public static ArchiveSelectionOption FromArchive(Archive archive)
    {
        return new(archive.ShortName,
            $"{archive.LongName} {archive.Uris.Sum(i => i.SizeInBytes).BytesToString()}");
    }

    public string ShortName { get; }

    public override string ToString()
    {
        return _title;
    }
}
