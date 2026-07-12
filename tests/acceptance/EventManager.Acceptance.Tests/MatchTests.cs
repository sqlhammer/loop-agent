using System.Net;
using System.Text.Json;
using Xunit;

namespace EventManager.Acceptance.Tests;

// Endpoints under test: GET /match/, GET /match/{id}/, POST /create_match/
// Match-type whitelist per GOAL: kata, combat.
public sealed class MatchTests : ApiTestBase
{
    // GOAL crit #3: empty database => GET /match/ returns 200 and an empty list.
    [Fact]
    public async Task Get_all_matches_on_empty_db_returns_empty_list()
    {
        var resp = await Client.GetAsync("/match/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    // GOAL crit #4: a valid match exists => GET /match/{id}/ returns 200 and a list with one
    // match containing all of the match object data points.
    [Fact]
    public async Task Get_match_by_id_returns_single_match_with_all_fields()
    {
        var create = await PostJson("/create_match/",
            """{"match_type":"kata","name":"Kata Round 1"}""");
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await BodyJson(create);
        var id = created.GetProperty("id").GetInt32();

        var resp = await Client.GetAsync($"/match/{id}/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var match = root[0];
        Assert.True(HasProps(match, "id", "match_type", "name", "event_id", "competitor_ids"),
            $"match is missing data points: {match}");
        Assert.Equal(id, match.GetProperty("id").GetInt32());
        Assert.Equal("kata", match.GetProperty("match_type").GetString());
    }

    // GOAL crit #11: empty database => POST /create_match/ returns 200 and the new match id
    // and match type.
    [Fact]
    public async Task Create_match_returns_ok_id_and_type()
    {
        var resp = await PostJson("/create_match/",
            """{"match_type":"combat","name":"Fight 1"}""");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.True(root.TryGetProperty("id", out var idEl), $"response has no id: {root}");
        Assert.True(idEl.GetInt32() > 0);
        Assert.Equal("combat", root.GetProperty("match_type").GetString());
    }

    // GOAL crit #12: whitelist is {kata, combat} => POST /create_match/ with type "BJJ"
    // returns 400 and the error "invalid match type".
    [Fact]
    public async Task Create_match_with_type_outside_whitelist_returns_400()
    {
        var resp = await PostJson("/create_match/",
            """{"match_type":"BJJ","name":"Grappling 1"}""");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await BodyText(resp);
        Assert.Contains("invalid match type", body);
    }
}
