using System.Text.Json;
using EventManager.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=eventmanager.db";

builder.Services.AddSingleton(new DatabaseConfig(connectionString));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

DatabaseInitializer.Initialize(connectionString);

EventEndpoints.Map(app);

app.Run();

public partial class Program { }
