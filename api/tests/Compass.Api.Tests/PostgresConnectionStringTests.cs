using System.Data.Common;
using Compass.Api.Infrastructure;

namespace Compass.Api.Tests;

public class PostgresConnectionStringTests
{
    private static DbConnectionStringBuilder Parse(string s) => new() { ConnectionString = s };

    [Fact]
    public void Url_is_converted_to_key_value_form()
    {
        var b = Parse(PostgresConnectionString.Normalize(
            "postgresql://owner:p%40ss;word@ep-x.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));
        Assert.Equal("ep-x.eu-central-1.aws.neon.tech", b["Host"]);
        Assert.Equal("5432", b["Port"].ToString());
        Assert.Equal("neondb", b["Database"]);
        Assert.Equal("owner", b["Username"]);
        Assert.Equal("p@ss;word", b["Password"]);
        Assert.Equal("Require", b["SSL Mode"]);
    }

    [Fact]
    public void Explicit_port_is_kept()
    {
        var b = Parse(PostgresConnectionString.Normalize("postgres://u:p@localhost:5433/compass"));
        Assert.Equal("5433", b["Port"].ToString());
        Assert.False(b.ContainsKey("SSL Mode"));
    }

    [Theory]
    [InlineData("\"Host=h;Database=d;Username=u;Password=p;SSL Mode=Require\"")]
    [InlineData("  Host=h;Database=d;Username=u;Password=p;SSL Mode=Require;  ")]
    [InlineData("'Host=h;Database=d;Username=u;Password=p;SSL Mode=Require;'")]
    public void Copy_paste_noise_is_removed(string raw)
    {
        Assert.Equal("Host=h;Database=d;Username=u;Password=p;SSL Mode=Require", PostgresConnectionString.Normalize(raw));
    }
}
