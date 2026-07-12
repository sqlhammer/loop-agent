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

        var ids = req.CompetitorIds;

        if (ids.Length > 0)
        {
            var placeholders = string.Join(",", ids.Select((_, i) => $"@c{i}"));
            using var check = conn.CreateCommand();
            check.CommandText = $"SELECT COUNT(*) FROM competitors WHERE id IN ({placeholders})";
            for (var i = 0; i < ids.Length; i++)
                check.Parameters.AddWithValue($"@c{i}", ids[i]);
            var found = (long)check.ExecuteScalar()!;
            if (found < ids.Length)
                return Results.BadRequest("unknown competitor id");
        }

        using var insertBracket = conn.CreateCommand();
        insertBracket.CommandText = "INSERT INTO brackets (event_id) VALUES (@eid) RETURNING id";
        insertBracket.Parameters.AddWithValue("@eid", req.EventId);
        var bracketId = (int)(long)insertBracket.ExecuteScalar()!;
        var bracketMatches = new List<BracketMatchDto>();

        for (var i = 0; i < ids.Length; i += 2)
        {
            var group = (i + 1 < ids.Length)
                ? new[] { ids[i], ids[i + 1] }
                : new[] { ids[i] };

            using var insertMatch = conn.CreateCommand();
            insertMatch.CommandText =
                "INSERT INTO bracket_matches (bracket_id, match_type, competitor_ids) " +
                "VALUES (@bid, @mt, @cids) RETURNING id";
            insertMatch.Parameters.AddWithValue("@bid", bracketId);
            insertMatch.Parameters.AddWithValue("@mt", req.MatchType);
            insertMatch.Parameters.AddWithValue("@cids", JsonSerializer.Serialize(group));
            var matchId = (int)(long)insertMatch.ExecuteScalar()!;

            bracketMatches.Add(new BracketMatchDto(matchId, req.MatchType, group));
        }

        return Results.Ok(new BracketDto(bracketId, req.EventId, bracketMatches.ToArray()));
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
