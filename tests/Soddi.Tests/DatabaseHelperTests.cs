using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Shouldly;
using Soddi.Providers.SqlServer;
using Soddi.Providers.Postgres;
using Xunit;

namespace Soddi.Tests;

public class SqlServerProviderTests
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
        var provider = new SqlServerProvider(new MockFileSystem());
        var master = provider.GetMasterConnectionString(connString);
        var database = provider.GetDatabaseConnectionString(connString, "Aviation");
        master.ShouldBe(expectedMaster);
        database.ShouldBe(expectedDb);
    }

    [Fact]
    public void Bad_connection_string_throws_exception()
    {
        var provider = new SqlServerProvider(new MockFileSystem());
        Should.Throw<SoddiException>(() =>
        {
            provider.GetMasterConnectionString("test");
        });
    }

    [Fact]
    public void Can_guess_database_name_when_we_tell_it_the_database_name()
    {
        var provider = new SqlServerProvider(new MockFileSystem());
        provider.GetDbNameFromPathOption("Aviation", "data-files").ShouldBe("Aviation");
    }

    [Fact]
    public void Can_guess_database_name_from_a_filename()
    {
        var provider = new SqlServerProvider(new MockFileSystem(
            new Dictionary<string, MockFileData>
            {
                { "aviation.stackexchange.7z", new MockFileData("") }
            }
        ));
        provider.GetDbNameFromPathOption("", "aviation.stackexchange.7z").ShouldBe("aviation.stackexchange");
    }

    [Fact]
    public void Can_guess_database_name_from_a_directory()
    {
        var provider = new SqlServerProvider(new MockFileSystem(
            new Dictionary<string, MockFileData>
            {
                { "aviation.stackexchange/file.7z", new MockFileData("") }
            }
        ));
        provider.GetDbNameFromPathOption("", "aviation.stackexchange").ShouldBe("aviation.stackexchange");
    }

    [Fact]
    public void Bad_path_throws_file_not_found()
    {
        var files = new Dictionary<string, MockFileData>
        {
            { "aviation.stackexchange/file.7z", new MockFileData("") }
        };
        var provider = new SqlServerProvider(new MockFileSystem(files));

        Should.Throw<FileNotFoundException>(() =>
        {
            provider.GetDbNameFromPathOption("", "dummy.stackexchange");
        });
    }
}

public class PostgresProviderTests
{
    [Theory]
    [InlineData("Host=localhost;Username=soddi;Password=pass",
        "Host=localhost;Username=soddi;Password=pass;Database=postgres",
        "Host=localhost;Username=soddi;Password=pass;Database=Aviation")]
    [InlineData("Host=localhost;Database=Northwind;Username=soddi;Password=pass",
        "Host=localhost;Database=postgres;Username=soddi;Password=pass",
        "Host=localhost;Database=Aviation;Username=soddi;Password=pass")]
    public void Can_get_master_and_db_connection(string connString, string expectedMaster, string expectedDb)
    {
        var provider = new PostgresProvider(new MockFileSystem());
        var master = provider.GetMasterConnectionString(connString);
        var database = provider.GetDatabaseConnectionString(connString, "Aviation");
        master.ShouldBe(expectedMaster);
        database.ShouldBe(expectedDb);
    }

    [Fact]
    public void Bad_connection_string_throws_exception()
    {
        var provider = new PostgresProvider(new MockFileSystem());
        Should.Throw<SoddiException>(() =>
        {
            provider.GetMasterConnectionString("invalid connection string");
        });
    }

    [Fact]
    public void Can_guess_database_name_when_we_tell_it_the_database_name()
    {
        var provider = new PostgresProvider(new MockFileSystem());
        provider.GetDbNameFromPathOption("Aviation", "data-files").ShouldBe("Aviation");
    }

    [Fact]
    public void Can_guess_database_name_from_a_filename()
    {
        var provider = new PostgresProvider(new MockFileSystem(
            new Dictionary<string, MockFileData>
            {
                { "aviation.stackexchange.7z", new MockFileData("") }
            }
        ));
        provider.GetDbNameFromPathOption("", "aviation.stackexchange.7z").ShouldBe("aviation.stackexchange");
    }

    [Fact]
    public void Can_guess_database_name_from_a_directory()
    {
        var provider = new PostgresProvider(new MockFileSystem(
            new Dictionary<string, MockFileData>
            {
                { "aviation.stackexchange/file.7z", new MockFileData("") }
            }
        ));
        provider.GetDbNameFromPathOption("", "aviation.stackexchange").ShouldBe("aviation.stackexchange");
    }

    [Fact]
    public void Bad_path_throws_file_not_found()
    {
        var files = new Dictionary<string, MockFileData>
        {
            { "aviation.stackexchange/file.7z", new MockFileData("") }
        };
        var provider = new PostgresProvider(new MockFileSystem(files));

        Should.Throw<FileNotFoundException>(() =>
        {
            provider.GetDbNameFromPathOption("", "dummy.stackexchange");
        });
    }
}
