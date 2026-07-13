using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EventManager.Api;

public static class BracketEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/bracket/", GetAll);
        app.MapGet("/bracket/{id}/", GetById);
        app.MapPost("/generate_bracket/", GenerateBracket);
    }

    private static IResult GetAll(DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryBrackets(conn, null));
    }

    private static IResult GetById(int id, DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryBrackets(conn, id));
    }

    private static IResult GenerateBracket(GenerateBracketRequest req, DatabaseConfig db)
    {
        using var conn = Open(db);

        if (!EventExists(conn, req.EventId))
            return Results.Text("unknown event id", statusCode: 400);

        var matchTypes = GetMatchTypesForEvent(conn, req.EventId);
        if (matchTypes.Count == 0)
            return Results.Text("insufficient matches", statusCode: 400);

        var competitorIds = GetAllCompetitorIds(conn);
        if (competitorIds.Count < 2)
            return Results.Text("insufficient competitors", statusCode: 400);

        var bracketId = InsertBracket(conn, req.EventId);
        var bracketMatches = InsertBracketMatches(conn, bracketId, competitorIds, matchTypes);

        return Results.Ok(new BracketDto(bracketId, req.EventId, bracketMatches.ToArray()));
    }

    private static bool EventExists(SqliteConnection conn, int eventId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM events WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", eventId);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    private static List<string> GetMatchTypesForEvent(SqliteConnection conn, int eventId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT match_type FROM matches WHERE event_id = @id ORDER BY id";
        cmd.Parameters.AddWithValue("@id", eventId);

        var matchTypes = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            matchTypes.Add(reader.GetString(0));
        return matchTypes;
    }

    private static List<int> GetAllCompetitorIds(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM competitors ORDER BY id";

        var competitorIds = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            competitorIds.Add(reader.GetInt32(0));
        return competitorIds;
    }

    private static int InsertBracket(SqliteConnection conn, int eventId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO brackets (event_id) VALUES (@eid) RETURNING id";
        cmd.Parameters.AddWithValue("@eid", eventId);
        return (int)(long)cmd.ExecuteScalar()!;
    }

    // Every competitor is placed (paired up, one leftover trio if the count is odd). Each
    // generated bracket match takes its type from the event's own matches, cycled
    // round-robin so bracket-match count isn't limited by how many matches were created.
    private static List<BracketMatchDto> InsertBracketMatches(
        SqliteConnection conn, int bracketId, List<int> competitorIds, List<string> matchTypes)
    {
        var bracketMatches = new List<BracketMatchDto>();
        for (var i = 0; i < competitorIds.Count; i += 2)
        {
            var group = (i + 1 < competitorIds.Count)
                ? new[] { competitorIds[i], competitorIds[i + 1] }
                : new[] { competitorIds[i] };
            var matchType = matchTypes[(i / 2) % matchTypes.Count];
            var matchId = InsertBracketMatch(conn, bracketId, matchType, group);

            bracketMatches.Add(new BracketMatchDto(matchId, matchType, group));
        }
        return bracketMatches;
    }

    private static int InsertBracketMatch(SqliteConnection conn, int bracketId, string matchType, int[] competitorIds)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO bracket_matches (bracket_id, match_type, competitor_ids) " +
            "VALUES (@bid, @mt, @cids) RETURNING id";
        cmd.Parameters.AddWithValue("@bid", bracketId);
        cmd.Parameters.AddWithValue("@mt", matchType);
        cmd.Parameters.AddWithValue("@cids", JsonSerializer.Serialize(competitorIds));
        return (int)(long)cmd.ExecuteScalar()!;
    }

    private static List<BracketDto> QueryBrackets(SqliteConnection conn, int? id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = id is null
            ? "SELECT id, event_id FROM brackets ORDER BY id"
            : "SELECT id, event_id FROM brackets WHERE id = @id";
        if (id is not null)
            cmd.Parameters.AddWithValue("@id", id.Value);

        var brackets = new List<(int Id, int EventId)>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                brackets.Add((reader.GetInt32(0), reader.GetInt32(1)));
        }

        var result = new List<BracketDto>();
        foreach (var (bId, eventId) in brackets)
        {
            var matches = QueryBracketMatches(conn, bId);
            result.Add(new BracketDto(bId, eventId, matches));
        }
        return result;
    }

    private static BracketMatchDto[] QueryBracketMatches(SqliteConnection conn, int bracketId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, match_type, competitor_ids FROM bracket_matches WHERE bracket_id = @bid ORDER BY id";
        cmd.Parameters.AddWithValue("@bid", bracketId);

        var list = new List<BracketMatchDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var cids = reader.IsDBNull(2)
                ? Array.Empty<int>()
                : JsonSerializer.Deserialize<int[]>(reader.GetString(2)) ?? Array.Empty<int>();
            list.Add(new BracketMatchDto(reader.GetInt32(0), reader.GetString(1), cids));
        }
        return list.ToArray();
    }

    private static SqliteConnection Open(DatabaseConfig db)
    {
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        return conn;
    }
}
