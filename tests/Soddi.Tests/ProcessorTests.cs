using System.Linq;
using Shouldly;
using Soddi.Services;
using Xunit;

namespace Soddi.Tests;

public class ProcessorTests
{
    private static readonly string[] s_expectedFiles = new[]
    {
        "badges", "comments", "posthistory", "postlinks", "posts", "tags", "users", "votes"
    };

    [Fact]
    public void Can_process_folder()
    {
        var processor = new FolderProcessor("test-files/eosio.meta.stackexchange.com/");

        processor.GetFiles().SelectMany(i => i).Select(i => i.fileName)
            .ShouldBe(s_expectedFiles.Select(i => i + ".xml"));
    }

    [Fact]
    public void Can_process_seven_zip()
    {
        var processor = new SequentialArchiveProcessor(new[] { "test-files/eosio.meta.stackexchange.com.7z" });

        processor.GetFiles().First().Select(i => i.fileName)
            .ShouldBe(s_expectedFiles.Select(i => i + ".xml"));
    }
}
