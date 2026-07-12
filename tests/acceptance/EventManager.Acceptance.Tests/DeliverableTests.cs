using System.Text.Json;
using Xunit;

namespace EventManager.Acceptance.Tests;

// These enforce the two hard deliverable constraints from GOAL "Stack & constraints":
// the server ships as a Docker container, and the API is exercisable via a Postman collection.
public sealed class DeliverableTests
{
    // GOAL constraint: "The application server runs on a docker container."
    [Fact]
    public void Dockerfile_exists_and_publishes_the_api()
    {
        var dockerfile = Path.Combine(RepoRoot(), "Dockerfile");
        Assert.True(File.Exists(dockerfile), $"Dockerfile not found at {dockerfile}");

        var text = File.ReadAllText(dockerfile);
        Assert.Contains("EventManager.Api", text);
        Assert.Contains("ENTRYPOINT", text, StringComparison.OrdinalIgnoreCase);
    }

    // GOAL constraint: "The application is accessible via REST API calls in a Postman collection"
    // covering every required endpoint.
    [Fact]
    public void Postman_collection_exists_and_covers_every_required_endpoint()
    {
        var collection = Path.Combine(RepoRoot(), "postman", "EventManager.postman_collection.json");
        Assert.True(File.Exists(collection), $"Postman collection not found at {collection}");

        var text = File.ReadAllText(collection);
        using (var doc = JsonDocument.Parse(text)) { /* must be valid JSON */ }

        string[] required =
        {
            "/event", "/match", "/bracket", "/competitor",
            "create_event", "create_match", "create_competitor", "generate_bracket",
        };
        foreach (var endpoint in required)
            Assert.True(text.Contains(endpoint, StringComparison.Ordinal),
                $"Postman collection does not reference endpoint '{endpoint}'");
    }

    // Walk up from the test binary to the repo root (the directory that holds GOAL.md).
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GOAL.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
