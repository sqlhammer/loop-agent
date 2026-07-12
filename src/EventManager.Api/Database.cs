using Microsoft.Data.Sqlite;

namespace EventManager.Api;

public record DatabaseConfig(string ConnectionString);

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        foreach (var sql in TableStatements())
            Exec(conn, sql);
    }

    private static IEnumerable<string> TableStatements() =>
    [
        """
        CREATE TABLE IF NOT EXISTS events (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            name        TEXT    NOT NULL UNIQUE,
            start_date  TEXT,
            location    TEXT
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS matches (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            match_type      TEXT    NOT NULL,
            name            TEXT,
            event_id        INTEGER,
            competitor_ids  TEXT    NOT NULL DEFAULT '[]'
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS competitors (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            name            TEXT    NOT NULL,
            styles          TEXT    NOT NULL DEFAULT '[]',
            birthdate       TEXT,
            weigh_in_weight REAL,
            weigh_in_units  TEXT
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS brackets (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            event_id    INTEGER NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS bracket_matches (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            bracket_id      INTEGER NOT NULL,
            match_type      TEXT    NOT NULL,
            competitor_ids  TEXT    NOT NULL DEFAULT '[]'
        )
        """
    ];

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
