using System.Net;
using System.Text.Json;
using Xunit;

namespace EventManager.Acceptance.Tests;

// Endpoints under test: GET /event/, GET /event/{id}/, POST /create_event/
public sealed class EventTests : ApiTestBase
{
    // GOAL crit #1: empty database => GET /event/ returns 200 and an empty list.
    [Fact]
    public async Task Get_all_events_on_empty_db_returns_empty_list()
    {
        var resp = await Client.GetAsync("/event/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(0, root.GetArrayLength());
    }

    // GOAL crit #2: an event exists => GET /event/{id}/ returns 200 and a list with one
    // event containing all of the event object data points.
    [Fact]
    public async Task Get_event_by_id_returns_single_event_with_all_fields()
    {
        var create = await PostJson("/create_event/",
            """{"name":"Regional Open","start_date":"2026-05-01","location":"Central Dojo"}""");
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await BodyJson(create);
        var id = created.GetProperty("id").GetInt32();

        var resp = await Client.GetAsync($"/event/{id}/");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var ev = root[0];
        Assert.True(HasProps(ev, "id", "name", "start_date", "location"),
            $"event is missing data points: {ev}");
        Assert.Equal(id, ev.GetProperty("id").GetInt32());
        Assert.Equal("Regional Open", ev.GetProperty("name").GetString());
        Assert.Equal("Central Dojo", ev.GetProperty("location").GetString());
    }

    // GOAL crit #9: empty database => POST /create_event/ returns 200 and the new event id.
    [Fact]
    public async Task Create_event_returns_ok_and_new_id()
    {
        var resp = await PostJson("/create_event/",
            """{"name":"Solo Cup","start_date":"2026-06-15","location":"North Gym"}""");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var root = await BodyJson(resp);
        Assert.True(root.TryGetProperty("id", out var idEl), $"response has no id: {root}");
        Assert.True(idEl.GetInt32() > 0);
    }

    // GOAL crit #10: an event named "Test Event 1" exists => creating another with the same
    // name returns 409 and an error that the event name already exists.
    [Fact]
    public async Task Create_event_with_duplicate_name_returns_409()
    {
        var first = await PostJson("/create_event/",
            """{"name":"Test Event 1","start_date":"2026-07-01","location":"Dojo A"}""");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var dup = await PostJson("/create_event/",
            """{"name":"Test Event 1","start_date":"2026-07-02","location":"Dojo B"}""");

        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var body = await BodyText(dup);
        Assert.Contains("an event with the name \"Test Event 1\" already exists", body);
    }
}
