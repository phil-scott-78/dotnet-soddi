using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Soddi
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
            public Uri Uri { get; }
            public long SizeInBytes { get; }

            public UriWithSize(Uri uri, long sizeInBytes)
            {
                Uri = uri;
                SizeInBytes = sizeInBytes;
            }
        }
    }

    public class AvailableArchiveParser
    {
        public async Task<IEnumerable<Archive>> Get(CancellationToken cancellationToken)
        {
            const string baseUrl = "https://archive.org/download/stackexchange/";
            const string downloadUrl = baseUrl + "stackexchange_files.xml";

            var client = new HttpClient();
            using var response = client
                .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).Result;
            var stream = await response.Content.ReadAsStreamAsync();

            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            if (doc.Root?.Document == null)
            {
                throw new Exception("Could not parse stackexchange_files.xml. XML Document was null");
            }

            var items = doc.Root.Elements()
                .Where(i => i.Element("format")?.Value == "7z")
                .Select(i => new
                {
                    Name = i.Attribute("name")?.Value ?? throw new Exception("Name not found"),
                    Size = i.Element("size")?.Value
                })
                .ToList();

            var names = items.Select(i => StripDashName(i.Name)).Distinct().ToArray();

            return names
                .Select(name => new
                {
                    name,
                    uris = items
                        .Where(i => StripDashName(i.Name) == name)
                        .Select(i => new Archive.UriWithSize(new Uri(baseUrl + i.Name), long.Parse(i.Size))).ToList()
                })
                .Select(name => new Archive(
                    name.name.Replace(".stackexchange.com.7z", ""),
                    name.name.Replace(".7z", ""),
                    name.uris));
        }

        private static string StripDashName(string input)
        {
            var index = input.IndexOf("-", StringComparison.Ordinal);
            return index < 0 ? input : input.Substring(0, index);
        }
    }
}
