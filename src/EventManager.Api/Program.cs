using EventManager.Api;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=eventmanager.db";

builder.Services.AddSingleton(new DatabaseConfig(connectionString));

var app = builder.Build();

DatabaseInitializer.Initialize(connectionString);

app.Run();

public partial class Program { }
