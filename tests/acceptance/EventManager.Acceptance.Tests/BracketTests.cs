using System.Net;
using System.Text.Json;
using Xunit;

namespace EventManager.Acceptance.Tests;

// Endpoints under test: GET /bracket/, GET /bracket/{id}/, POST /generate_bracket/
public sealed class BracketTests : ApiTestBase
{
    // GOAL crit #5: empty database => GET /bracket/ returns 200 and an empty list.
    [Fact]
    public async Task Get_all_brackets_on_empty_db_returns_empty_list()
    {
        var resp = await Client.GetAsync("/bracket/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    // GOAL crit #6: a valid bracket exists => GET /bracket/{id}/ returns 200 and a list with
    // one bracket containing all of the bracket data points INCLUDING the groupings of
    // competitors per match.
    [Fact]
    public async Task Get_bracket_by_id_returns_single_bracket_with_competitor_groupings()
    {
        var (bracketId, competitorIds) = await SeedBracketAsync();

        var resp = await Client.GetAsync($"/bracket/{bracketId}/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var bracket = root[0];
        AssertBracketShape(bracket, bracketId, competitorIds);
    }

    // GOAL crit #14: event #1 + three valid matches + eight valid unique competitors =>
    // POST /generate_bracket/ returns 200 and the bracket data along with its newly
    // generated bracket id.
    [Fact]
    public async Task Generate_bracket_returns_ok_with_new_id_and_groupings()
    {
        var eventId = await CreateEventAsync("Championship");
        // three valid matches, as the GOAL scenario specifies
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK,
                (await PostJson("/create_match/", $$"""{"match_type":"combat","name":"Seed {{i}}"}""")).StatusCode);

        var competitorIds = await CreateCompetitorsAsync(8);

        var resp = await PostJson("/generate_bracket/",
            $$"""{"event_id":{{eventId}},"competitor_ids":[{{string.Join(",", competitorIds)}}],"match_type":"combat"}""");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var bracket = await BodyJson(resp);
        Assert.True(bracket.TryGetProperty("id", out var idEl), $"bracket has no id: {bracket}");
        Assert.True(idEl.GetInt32() > 0);
        AssertBracketShape(bracket, idEl.GetInt32(), competitorIds);
    }

    // ASSUMPTIONS.md #14: an empty database has no real competitors, so
    // POST /generate_bracket/ must not fabricate a bracket from competitor ids that don't
    // exist — it rejects with 400 instead of persisting a bracket for phantom competitors.
    [Fact]
    public async Task Generate_bracket_with_no_competitors_in_db_rejects_unknown_ids()
    {
        var eventId = await CreateEventAsync("Championship");

        var resp = await PostJson("/generate_bracket/",
            $$"""{"event_id":{{eventId}},"competitor_ids":[1,2,3,4,5,6,7,8],"match_type":"combat"}""");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await BodyText(resp);
        Assert.Contains("unknown competitor id", body);

        // No phantom bracket should have been persisted.
        var brackets = await BodyJson(await Client.GetAsync("/bracket/"));
        Assert.Equal(0, brackets.GetArrayLength());
    }

    // A bracket carries its id, its event id, and matches whose per-match competitor
    // groupings together cover every competitor that was entered.
    private static void AssertBracketShape(JsonElement bracket, int expectedId, IReadOnlyCollection<int> competitorIds)
    {
        Assert.True(HasProps(bracket, "id", "event_id", "matches"),
            $"bracket is missing data points: {bracket}");
        Assert.Equal(expectedId, bracket.GetProperty("id").GetInt32());

        var matches = bracket.GetProperty("matches");
        Assert.Equal(JsonValueKind.Array, matches.ValueKind);
        Assert.True(matches.GetArrayLength() > 0, "bracket has no matches");

        var grouped = new HashSet<int>();
        foreach (var match in matches.EnumerateArray())
        {
            Assert.True(match.TryGetProperty("competitor_ids", out var ids),
                $"match has no competitor grouping: {match}");
            Assert.Equal(JsonValueKind.Array, ids.ValueKind);
            foreach (var c in ids.EnumerateArray())
                grouped.Add(c.GetInt32());
        }

        foreach (var id in competitorIds)
            Assert.Contains(id, grouped);
    }

    private async Task<(int bracketId, List<int> competitorIds)> SeedBracketAsync()
    {
        var eventId = await CreateEventAsync("Seeded Event");
        var competitorIds = await CreateCompetitorsAsync(8);

        var resp = await PostJson("/generate_bracket/",
            $$"""{"event_id":{{eventId}},"competitor_ids":[{{string.Join(",", competitorIds)}}],"match_type":"combat"}""");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var bracketId = (await BodyJson(resp)).GetProperty("id").GetInt32();
        return (bracketId, competitorIds);
    }

    private async Task<int> CreateEventAsync(string name)
    {
        var resp = await PostJson("/create_event/",
            $$"""{"name":"{{name}}","start_date":"2026-08-01","location":"Arena"}""");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await BodyJson(resp)).GetProperty("id").GetInt32();
    }

    private async Task<List<int>> CreateCompetitorsAsync(int count)
    {
        // Non-interpolated template (nested JSON braces don't mix with raw-string interpolation).
        const string template =
            """{"name":"__NAME__","styles":["karate"],"birthdate":"01-01-2000","last_weigh_in":{"weight":150.0,"units":"lbs"}}""";

        var ids = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var resp = await PostJson("/create_competitor/", template.Replace("__NAME__", $"Comp {i}"));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            ids.Add((await BodyJson(resp)).GetProperty("id").GetInt32());
        }
        return ids;
    }
}
