using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Shouldly;
using Soddi.Services;
using Xunit;

namespace Soddi.Tests.DatabaseHelperTests
{
    public class CreateProcessorTests
    {
        private static readonly string[] s_expectedFiles = new[]
        {
            "badges", "comments", "posthistory", "postlinks", "posts", "tags", "users", "votes"
        };

        [Fact]
        public void Missing_path_throws_exception()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                {"archive.7z", new MockFileData("")}
            });

            var dbHelper = new DatabaseHelpers(mockFileSystem);
            Should.Throw<SoddiException>(() =>
            {
                dbHelper.VerifyAndCreateProcessor("not-archive.7z");
            });
        }

        [Fact]
        public void Archive_processor_is_created_for_7z_file()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                {"archive.7z", new MockFileData("")}
            });

            var dbHelper = new DatabaseHelpers(mockFileSystem);
            var processor = dbHelper.VerifyAndCreateProcessor("archive.7z");
            processor.ShouldBeOfType<ArchiveProcessor>();
        }

        [Fact]
        public void Non_7z_files_throw_exceptions()
        {
            var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>()
            {
                {"archive.zip", new MockFileData("")}
            });

            var dbHelper = new DatabaseHelpers(mockFileSystem);

            Should.Throw<SoddiException>(() =>
                {
                    var processor = dbHelper.VerifyAndCreateProcessor("archive.zip");
                }
            );
        }

        [Fact]
        public void Directory_with_7z_files_returns_archive_processor()
        {
            var files = s_expectedFiles.ToDictionary(s => "archive/" + s + ".7z", s => new MockFileData("test"));
            var mockFileSystem = new MockFileSystem(files);

            var dbHelper = new DatabaseHelpers(mockFileSystem);

            var processor = dbHelper.VerifyAndCreateProcessor("archive");
            processor.ShouldBeOfType<ArchiveProcessor>();
        }


        [Fact]
        public void Directory_with_xml_files_returns_folder_processor()
        {
            var files = s_expectedFiles.ToDictionary(s => "archive/" + s + ".xml", s => new MockFileData("test"));
            var mockFileSystem = new MockFileSystem(files);

            var dbHelper = new DatabaseHelpers(mockFileSystem);

            var processor = dbHelper.VerifyAndCreateProcessor("archive");
            processor.ShouldBeOfType<FolderProcessor>();
        }


        [Fact]
        public void Directory_with_missing_7z_files_throws_exception()
        {
            var mockFileSystem = new MockFileSystem(new
                Dictionary<string, MockFileData> {{"archive/blogs.xml", new MockFileData("")}}
            );

            var dbHelper = new DatabaseHelpers(mockFileSystem);

            Should.Throw<SoddiException>(() =>
            {
                dbHelper.VerifyAndCreateProcessor("archive");
            });
        }

        [Fact]
        public void Directory_with_both_xml_and_7z_files_returns_archive_processor()
        {
            var sevenFiles = s_expectedFiles.ToDictionary(s => "archive/" + s + ".7z", s => new MockFileData("test"));
            var xmlFiles = s_expectedFiles.ToDictionary(s => "archive/" + s + ".xml", s => new MockFileData("test"));

            var files = sevenFiles.Concat(xmlFiles).ToDictionary(i => i.Key, i => i.Value);

            var mockFileSystem = new MockFileSystem(files);

            var dbHelper = new DatabaseHelpers(mockFileSystem);

            Should.Throw<SoddiException>(() =>
            {
                dbHelper.VerifyAndCreateProcessor("archive");
            });
        }
    }
}
