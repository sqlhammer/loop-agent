using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EventManager.Api;

public static class MatchEndpoints
{
    private static readonly HashSet<string> ValidMatchTypes = ["kata", "combat"];

    public static void Map(WebApplication app)
    {
        app.MapGet("/match/", GetAll);
        app.MapGet("/match/{id}/", GetById);
        app.MapPost("/create_match/", Create);
    }

    private static IResult GetAll(DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryMatches(conn, null));
    }

    private static IResult GetById(int id, DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryMatches(conn, id));
    }

    private static IResult Create(CreateMatchRequest req, DatabaseConfig db)
    {
        if (!ValidMatchTypes.Contains(req.MatchType))
            return Results.Text("invalid match type", statusCode: 400);

        using var conn = Open(db);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO matches (match_type, name, event_id, competitor_ids) VALUES (@t, @n, @e, '[]') RETURNING id";
        cmd.Parameters.AddWithValue("@t", req.MatchType);
        cmd.Parameters.AddWithValue("@n", req.Name ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@e", req.EventId.HasValue ? (object)req.EventId.Value : DBNull.Value);

        var id = (long)cmd.ExecuteScalar()!;
        return Results.Ok(new MatchDto((int)id, req.MatchType, req.Name ?? "", req.EventId, []));
    }

    private static List<MatchDto> QueryMatches(SqliteConnection conn, int? id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = id is null
            ? "SELECT id, match_type, name, event_id, competitor_ids FROM matches ORDER BY id"
            : "SELECT id, match_type, name, event_id, competitor_ids FROM matches WHERE id = @id";
        if (id is not null)
            cmd.Parameters.AddWithValue("@id", id.Value);

        var list = new List<MatchDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var competitorIds = reader.IsDBNull(4)
                ? []
                : JsonSerializer.Deserialize<int[]>(reader.GetString(4)) ?? [];

            list.Add(new MatchDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                competitorIds));
        }
        return list;
    }

    private static SqliteConnection Open(DatabaseConfig db)
    {
        var conn = new SqliteConnection(db.ConnectionString);
        conn.Open();
        return conn;
    }
}
