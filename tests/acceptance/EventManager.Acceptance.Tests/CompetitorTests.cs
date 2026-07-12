using System.Net;
using System.Text.Json;
using Xunit;

namespace EventManager.Acceptance.Tests;

// Endpoints under test: GET /competitor/, GET /competitor/{id}/, POST /create_competitor/
public sealed class CompetitorTests : ApiTestBase
{
    private const string SampleCompetitor =
        """{"name":"Test comp 1","styles":["karate","BJJ"],"birthdate":"09-01-2000","last_weigh_in":{"weight":160.4,"units":"lbs"}}""";

    // GOAL crit #7: empty database => GET /competitor/ returns 200 and an empty list.
    [Fact]
    public async Task Get_all_competitors_on_empty_db_returns_empty_list()
    {
        var resp = await Client.GetAsync("/competitor/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    // GOAL crit #8: a valid competitor exists => GET /competitor/{id}/ returns 200 and a list
    // with one competitor containing all of the competitor object data points.
    [Fact]
    public async Task Get_competitor_by_id_returns_single_competitor_with_all_fields()
    {
        var create = await PostJson("/create_competitor/", SampleCompetitor);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var id = (await BodyJson(create)).GetProperty("id").GetInt32();

        var resp = await Client.GetAsync($"/competitor/{id}/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var comp = root[0];
        Assert.True(HasProps(comp, "id", "name", "styles", "birthdate", "last_weigh_in"),
            $"competitor is missing data points: {comp}");
        Assert.Equal("Test comp 1", comp.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Array, comp.GetProperty("styles").ValueKind);
        var weighIn = comp.GetProperty("last_weigh_in");
        Assert.True(HasProps(weighIn, "weight", "units"),
            $"last_weigh_in is missing data points: {weighIn}");
    }

    // GOAL crit #13: POST /create_competitor/ with the sample body returns 200 and the
    // competitor data along with its newly generated competitor id.
    [Fact]
    public async Task Create_competitor_returns_ok_with_data_and_new_id()
    {
        var resp = await PostJson("/create_competitor/", SampleCompetitor);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.True(root.TryGetProperty("id", out var idEl), $"response has no id: {root}");
        Assert.True(idEl.GetInt32() > 0);

        Assert.Equal("Test comp 1", root.GetProperty("name").GetString());

        var styles = root.GetProperty("styles").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("karate", styles);
        Assert.Contains("BJJ", styles);

        Assert.Equal("09-01-2000", root.GetProperty("birthdate").GetString());
        var weighIn = root.GetProperty("last_weigh_in");
        Assert.Equal(160.4, weighIn.GetProperty("weight").GetDouble(), 3);
        Assert.Equal("lbs", weighIn.GetProperty("units").GetString());
    }
}
