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

app.MapPost(
    "/create_event/",
    async (CreateEventRequest req, AppDbContext db) =>
    {
        var ev = new Event { Name = req.Name };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return Results.Ok(new { event_id = ev.Id });
    }
);

app.Run();

record CreateEventRequest(string Name);
