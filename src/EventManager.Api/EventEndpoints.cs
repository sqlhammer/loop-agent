using Microsoft.Data.Sqlite;

namespace EventManager.Api;

public static class EventEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/event/", GetAll);
        app.MapGet("/event/{id}/", GetById);
        app.MapPost("/create_event/", Create);
    }

    private static IResult GetAll(DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryEvents(conn, null));
    }

    private static IResult GetById(int id, DatabaseConfig db)
    {
        using var conn = Open(db);
        return Results.Ok(QueryEvents(conn, id));
    }

    private static IResult Create(CreateEventRequest req, DatabaseConfig db)
    {
        using var conn = Open(db);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO events (name, start_date, location) VALUES (@n, @d, @l) RETURNING id";
        cmd.Parameters.AddWithValue("@n", req.Name);
        cmd.Parameters.AddWithValue("@d", req.StartDate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@l", req.Location ?? (object)DBNull.Value);

        try
        {
            var id = (long)cmd.ExecuteScalar()!;
            return Results.Ok(new EventDto((int)id, req.Name, req.StartDate ?? "", req.Location ?? ""));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Results.Text($"an event with the name \"{req.Name}\" already exists", statusCode: 409);
        }
    }

    private static List<EventDto> QueryEvents(SqliteConnection conn, int? id)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = id is null
            ? "SELECT id, name, start_date, location FROM events ORDER BY id"
            : "SELECT id, name, start_date, location FROM events WHERE id = @id";
        if (id is not null)
            cmd.Parameters.AddWithValue("@id", id.Value);

        var list = new List<EventDto>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new EventDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? "" : reader.GetString(3)));
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
