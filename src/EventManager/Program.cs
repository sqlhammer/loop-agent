using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EventManager;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=eventmanager.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.MapGet("/event/", async (AppDbContext db) =>
    Results.Ok(await db.Events.ToListAsync()));

app.MapGet(
    "/event/{id}/",
    async (int id, AppDbContext db) =>
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null)
            return Results.NotFound();
        return Results.Ok(new[] { ev });
    }
);

app.MapPost(
    "/create_event/",
    async (CreateEventRequest req, AppDbContext db) =>
    {
        var exists = await db.Events.AnyAsync(e => e.Name == req.Name);
        if (exists)
            return Results.Conflict(
                new { error = $"an event with the name \"{req.Name}\" already exists" }
            );
        var ev = new Event { Name = req.Name };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return Results.Ok(new { event_id = ev.Id });
    }
);

app.MapGet("/match/", async (AppDbContext db) =>
    Results.Ok(await db.Matches.ToListAsync()));

app.MapGet(
    "/match/{id}/",
    async (int id, AppDbContext db) =>
    {
        var match = await db.Matches.FindAsync(id);
        if (match is null)
            return Results.NotFound();
        return Results.Ok(new[] { match });
    }
);

app.MapPost(
    "/create_match/",
    async (CreateMatchRequest req, AppDbContext db) =>
    {
        var allowed = new[] { "kata", "combat" };
        if (!allowed.Contains(req.Type, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "invalid match type" });
        var match = new Match { Type = req.Type.ToLowerInvariant(), EventId = req.EventId };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return Results.Ok(new { match_id = match.Id, type = match.Type });
    }
);

app.MapGet(
    "/competitor/",
    async (AppDbContext db) =>
    {
        var list = await db.Competitors.ToListAsync();
        return Results.Ok(list.Select(ToCompetitorDto));
    }
);

app.MapGet(
    "/competitor/{id}/",
    async (int id, AppDbContext db) =>
    {
        var c = await db.Competitors.FindAsync(id);
        if (c is null)
            return Results.NotFound();
        return Results.Ok(new[] { ToCompetitorDto(c) });
    }
);

app.MapPost(
    "/create_competitor/",
    async (CreateCompetitorRequest req, AppDbContext db) =>
    {
        var c = new Competitor
        {
            Name = req.Name,
            StylesJson = JsonSerializer.Serialize(req.Styles ?? Array.Empty<string>()),
            Birthdate = req.Birthdate ?? string.Empty,
            LastWeighInWeight = req.LastWeighIn?.Weight ?? 0,
            LastWeighInUnits = req.LastWeighIn?.Units ?? string.Empty,
        };
        db.Competitors.Add(c);
        await db.SaveChangesAsync();
        return Results.Ok(
            new
            {
                competitor_id = c.Id,
                name = c.Name,
                styles = req.Styles ?? Array.Empty<string>(),
                birthdate = c.Birthdate,
                last_weigh_in = new { weight = c.LastWeighInWeight, units = c.LastWeighInUnits },
            }
        );
    }
);

app.Run();

static object ToCompetitorDto(Competitor c) =>
    new
    {
        id = c.Id,
        name = c.Name,
        styles =
            JsonSerializer.Deserialize<string[]>(c.StylesJson) ?? Array.Empty<string>(),
        birthdate = c.Birthdate,
        last_weigh_in = new { weight = c.LastWeighInWeight, units = c.LastWeighInUnits },
    };

record CreateEventRequest(string Name);
record CreateMatchRequest(string Type, int EventId);
record WeighInRequest(double Weight, string Units);
record CreateCompetitorRequest(
    string Name,
    string[] Styles,
    string Birthdate,
    WeighInRequest? LastWeighIn
);
