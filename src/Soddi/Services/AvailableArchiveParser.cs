using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Soddi.Services
{
    public class Archive
    {
        public string ShortName { get; }
        public string LongName { get; }
        public List<UriWithSize> Uris { get; }

        public Archive(string shortName, string longName, List<UriWithSize> uris)
        {
            ShortName = shortName;
            LongName = longName;
            Uris = uris;
        }

        public class UriWithSize
        {
            private readonly IFileSystem _fileSystem;

            public Uri Uri { get; }
            public long SizeInBytes { get; }

            public string Description(bool includeFileSize = true)
            {
                var fileName = _fileSystem.Path.GetFileName(Uri.AbsolutePath);
                return includeFileSize ? $"{fileName} ({SizeInBytes.BytesToString()})" : fileName;
            }

            public UriWithSize(Uri uri, long sizeInBytes, IFileSystem? fileSystem = null)
            {
                Uri = uri;
                SizeInBytes = sizeInBytes;
                _fileSystem = fileSystem ?? new FileSystem();
            }
        }
    }

    public class AvailableArchiveParser
    {
        public async Task<IEnumerable<Archive>> Get(CancellationToken cancellationToken)
        {
            const string BaseUrl = "https://archive.org/download/stackexchange/";
            const string DownloadUrl = BaseUrl + "stackexchange_files.xml";

            var client = new HttpClient();
            using var response = client
                .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).Result;
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

        private static string StripDashName(string input)
        {
            var index = input.IndexOf("-", StringComparison.Ordinal);
            return index < 0 ? input : input.Substring(0, index);
        }
    }
}
