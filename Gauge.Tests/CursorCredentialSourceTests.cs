using System.Text;
using Gauge.Models;
using Gauge.Services;
using Microsoft.Data.Sqlite;

namespace Gauge.Tests;

public sealed class CursorCredentialSourceTests
{
    [Fact]
    public async Task MissingDatabaseReportsMissing()
    {
        var source = new CursorCredentialSource(() => Path.Combine(Path.GetTempPath(), "no_such_gauge_cursor.vscdb"));
        var result = await source.ReadAsync(ToolKind.Cursor);
        Assert.Equal(CredentialReadStatus.Missing, result.Status);
    }

    [Fact]
    public async Task ReadsTokenAndUserIdFromVscdb()
    {
        using var db = new TempVscdb();
        var jwt = MakeJwt("auth0|user_abc123", DateTimeOffset.UtcNow.AddHours(2));
        db.Insert("cursorAuth/accessToken", jwt);

        var result = await new CursorCredentialSource(() => db.Path).ReadAsync(ToolKind.Cursor);

        Assert.Equal(CredentialReadStatus.Available, result.Status);
        Assert.Equal(jwt, result.Credential!.AccessToken);
        Assert.Equal("user_abc123", result.Credential.AccountId); // last segment of sub
        Assert.NotNull(result.Credential.ExpiresAt);
    }

    [Fact]
    public async Task ExpiredTokenReportsInvalid()
    {
        using var db = new TempVscdb();
        db.Insert("cursorAuth/accessToken", MakeJwt("user_x", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var result = await new CursorCredentialSource(() => db.Path).ReadAsync(ToolKind.Cursor);

        Assert.Equal(CredentialReadStatus.Invalid, result.Status);
    }

    [Fact]
    public async Task OtherToolFallsThrough()
    {
        using var db = new TempVscdb();
        db.Insert("cursorAuth/accessToken", MakeJwt("user_x", DateTimeOffset.UtcNow.AddHours(1)));

        var result = await new CursorCredentialSource(() => db.Path).ReadAsync(ToolKind.ClaudeCode);

        Assert.Equal(CredentialReadStatus.Missing, result.Status);
    }

    private static string MakeJwt(string sub, DateTimeOffset exp)
    {
        static string Enc(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = Enc("{\"alg\":\"none\"}");
        var payload = Enc($"{{\"sub\":\"{sub}\",\"exp\":{exp.ToUnixTimeSeconds()}}}");
        return $"{header}.{payload}.signature";
    }

    private sealed class TempVscdb : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GaugeCursorTest_" + Guid.NewGuid().ToString("N") + ".vscdb");

        public TempVscdb()
        {
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE ItemTable (key TEXT PRIMARY KEY, value TEXT);";
            command.ExecuteNonQuery();
        }

        public void Insert(string key, string value)
        {
            using var connection = new SqliteConnection($"Data Source={Path}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO ItemTable (key, value) VALUES ($k, $v);";
            command.Parameters.AddWithValue("$k", key);
            command.Parameters.AddWithValue("$v", value);
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(Path)) File.Delete(Path); } catch { /* best effort */ }
        }
    }
}
