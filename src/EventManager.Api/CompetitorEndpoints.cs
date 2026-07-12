using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace EventManager.Api;

public static class CompetitorEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/competitor/", GetAll);
        app.MapGet("/competitor/{id}/", GetById);
        app.MapPost("/create_competitor/", Create);
    }

    private static IResult GetAll(DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryCompetitors(conn, null));
    }

    private static IResult GetById(int id, DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryCompetitors(conn, id));
    }

    private static IResult Create(CreateCompetitorRequest req, DatabaseConfig db)
    {
        using var conn = Open(db);
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO competitors (name, styles, birthdate, weigh_in_weight, weigh_in_units) " +
            "VALUES (@n, @s, @b, @w, @u) RETURNING id";
        cmd.Parameters.AddWithValue("@n", req.Name);
        cmd.Parameters.AddWithValue("@s", JsonSerializer.Serialize(req.Styles));
        cmd.Parameters.AddWithValue("@b", req.Birthdate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@w", req.LastWeighIn.Weight);
        cmd.Parameters.AddWithValue("@u", req.LastWeighIn.Units);

        var id = (long)cmd.ExecuteScalar()!;
        return Results.Ok(new CompetitorDto((int)id, req.Name, req.Styles, req.Birthdate ?? "", req.LastWeighIn));
    }

    private static List<CompetitorDto> QueryCompetitors(SqliteConnection conn, int? id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = id is null
            ? "SELECT id, name, styles, birthdate, weigh_in_weight, weigh_in_units FROM competitors ORDER BY id"
            : "SELECT id, name, styles, birthdate, weigh_in_weight, weigh_in_units FROM competitors WHERE id = @id";
        if (id is not null)
            cmd.Parameters.AddWithValue("@id", id.Value);

        var list = new List<CompetitorDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var styles = reader.IsDBNull(2)
                ? []
                : JsonSerializer.Deserialize<string[]>(reader.GetString(2)) ?? [];

            var weighIn = new WeighInDto(
                reader.IsDBNull(4) ? 0.0 : reader.GetDouble(4),
                reader.IsDBNull(5) ? "" : reader.GetString(5));

            list.Add(new CompetitorDto(
                reader.GetInt32(0),
                reader.GetString(1),
                styles,
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                weighIn));
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
