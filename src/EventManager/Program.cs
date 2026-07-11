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

app.Run();

record CreateEventRequest(string Name);
