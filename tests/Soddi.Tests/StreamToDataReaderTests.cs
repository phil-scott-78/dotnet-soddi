using System;
using System.Collections.Generic;
using System.IO;
using Shouldly;
using Soddi.Services;
using Xunit;

namespace Soddi.Tests;

public class StreamToDataReaderTests
{
    [Fact]
    public void Tags_are_sent_when_found()
    {
        using var stream = File.OpenRead("test-files/eosio.meta.stackexchange.com/Posts.xml");
        List<(int PostId, string Tags)> postAndTags = new();

        using var dr = stream.AsDataReader("posts.xml", v =>
        {
            postAndTags.Add(v);
        });

        while (dr.Read())
        {
        }

        postAndTags.ShouldNotBeEmpty();
    }

    [Fact]
    public void Can_parse_schema()
    {
        using var stream = File.OpenRead("test-files/eosio.meta.stackexchange.com/Badges.xml");
        using var dr = stream.AsDataReader("badges.xml");

        dr.FieldCount.ShouldBe(4);

        var id = dr.GetOrdinal("Id");
        var userId = dr.GetOrdinal("UserId");
        var name = dr.GetOrdinal("Name");
        var date = dr.GetOrdinal("Date");

        id.ShouldBeGreaterThanOrEqualTo(0);
        userId.ShouldBeGreaterThanOrEqualTo(0);
        name.ShouldBeGreaterThanOrEqualTo(0);
        date.ShouldBeGreaterThanOrEqualTo(0);

        dr.GetFieldType(id).ShouldBe(typeof(int));
        dr.GetFieldType(userId).ShouldBe(typeof(int));
        dr.GetFieldType(name).ShouldBe(typeof(string));
        dr.GetFieldType(date).ShouldBe(typeof(DateTime));

        dr.GetDataTypeName(id).ShouldBe("Int32");
        dr.GetDataTypeName(userId).ShouldBe("Int32");
        dr.GetDataTypeName(name).ShouldBe("String");
        dr.GetDataTypeName(date).ShouldBe("DateTime");
    }

    [Fact]
    public void Can_read_data()
    {
        using var stream = File.OpenRead("test-files/eosio.meta.stackexchange.com/Badges.xml");
        using var dr = stream.AsDataReader("badges.xml");

        while (dr.Read())
        {
            var idOrdinal = dr.GetOrdinal("Id");
            dr.IsDBNull(idOrdinal).ShouldBeFalse();
            int.Parse((string)dr.GetValue(idOrdinal)).ShouldBeGreaterThanOrEqualTo(0);
        }
    }
}