using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Shouldly;
using Soddi.Services;
using Xunit;

namespace Soddi.Tests
{
    public class DatabaseHelperTests
    {
        [Theory]
        [InlineData("Server=(local);Integrated Security=True",
            "Data Source=(local);Initial Catalog=master;Integrated Security=True",
            "Data Source=(local);Initial Catalog=Aviation;Integrated Security=True")]
        [InlineData("Server=(local);Initial Catalog=Aviation;Integrated Security=True",
            "Data Source=(local);Initial Catalog=master;Integrated Security=True",
            "Data Source=(local);Initial Catalog=Aviation;Integrated Security=True")]
        [InlineData("Server=(local);Initial Catalog=Northwind;Integrated Security=True",
            "Data Source=(local);Initial Catalog=master;Integrated Security=True",
            "Data Source=(local);Initial Catalog=Aviation;Integrated Security=True")]
        [InlineData("Server=(local);Initial Catalog=Northwind;User Id=myUsername;Password=myPassword;",
            "Data Source=(local);Initial Catalog=master;User ID=myUsername;Password=myPassword",
            "Data Source=(local);Initial Catalog=Aviation;User ID=myUsername;Password=myPassword")]
        public void Can_get_master_and_db_connection(string connString, string expectedMaster, string expectedDb)
        {
            var db = new DatabaseHelper(new MockFileSystem());
            var c = db.GetMasterAndDbConnectionStrings(connString, "Aviation");
            c.master.ShouldBe(expectedMaster);
            c.database.ShouldBe(expectedDb);
        }

        [Fact]
        public void Bad_connection_string_throws_exception()
        {
            var db = new DatabaseHelper(new MockFileSystem());
            Should.Throw<SoddiException>(() =>
            {
                db.GetMasterAndDbConnectionStrings("test", "db");
            });
        }

        [Fact]
        public void Can_guess_database_name_when_we_tell_it_the_database_name()
        {
            var db = new DatabaseHelper(new MockFileSystem());
            db.GetDbNameFromPathOption("Aviation", "data-files").ShouldBe("Aviation");
        }

        [Fact]
        public void Can_guess_database_name_from_a_filename()
        {
            var db = new DatabaseHelper(new MockFileSystem(
                new Dictionary<string, MockFileData>
                {
                    { "aviation.stackexchange.7z", new MockFileData("") }
                }
            ));
            db.GetDbNameFromPathOption("", "aviation.stackexchange.7z").ShouldBe("aviation.stackexchange");
        }

        [Fact]
        public void Can_guess_database_name_from_a_directory()
        {
            var db = new DatabaseHelper(new MockFileSystem(
                new Dictionary<string, MockFileData>
                {
                    { "aviation.stackexchange/file.7z", new MockFileData("") }
                }
            ));
            db.GetDbNameFromPathOption("", "aviation.stackexchange").ShouldBe("aviation.stackexchange");
        }

        [Fact]
        public void Bad_path_throws_file_not_found()
        {
            var files = new Dictionary<string, MockFileData>
            {
                { "aviation.stackexchange/file.7z", new MockFileData("") }
            };
            var db = new DatabaseHelper(new MockFileSystem(
                files
            ));

            Should.Throw<FileNotFoundException>(() =>
            {
                db.GetDbNameFromPathOption("", "dummy.stackexchange");
            });
        }
    }
}
