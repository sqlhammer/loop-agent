using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventManager.Acceptance.Tests;

// Spins up the real EventManager.Api HTTP pipeline in-process against a fresh, empty
// SQLite database (a unique temp file per test), so IDs are predictable (first insert => 1)
// and every test starts from an empty database exactly as the GOAL scenarios require.
public sealed class EventManagerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath =
        Path.Combine(Path.GetTempPath(), $"em-acceptance-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The API must read its connection string from configuration key
        // "ConnectionStrings:Default" and create the schema on startup.
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { /* best effort cleanup */ }
        }
    }
}

public abstract class ApiTestBase : IAsyncLifetime
{
    protected readonly EventManagerApiFactory Factory = new();
    protected HttpClient Client = default!;

    // Every acceptance test issues raw JSON exactly as the GOAL describes and inspects
    // the raw response, so the contract (snake_case field names, status codes) is pinned.
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    protected Task<HttpResponseMessage> PostJson(string url, string json)
        => Client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

    protected static async Task<string> BodyText(HttpResponseMessage resp)
        => await resp.Content.ReadAsStringAsync();

    protected static async Task<JsonElement> BodyJson(HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    // True when a JSON object exposes every one of the named properties.
    protected static bool HasProps(JsonElement obj, params string[] names)
        => obj.ValueKind == JsonValueKind.Object && names.All(n => obj.TryGetProperty(n, out _));
}
