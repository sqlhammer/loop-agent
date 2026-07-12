Plan generated. Awaiting build loop.

## Iteration 1 — Task 1: Scaffold EventManager.Api project
Created `src/EventManager.Api/EventManager.Api.csproj` (Microsoft.NET.Sdk.Web, net10.0) and `Program.cs` with minimal host wiring and `public partial class Program {}`. Fixed CRLF line endings via `dotnet format` to satisfy `.editorconfig`. `verify.ps1 -Gate` exits 0 (build + lint green).
Next iteration should do Task 2: Add SQLite persistence wiring (read connection string from `ConnectionStrings:Default`, open connection, create tables on startup).

## Iteration 2 — Task 2: Add SQLite persistence wiring
Added `Microsoft.Data.Sqlite` 10.0.9 package; created `src/EventManager.Api/Database.cs` with `DatabaseConfig` record (holds connection string, registered as singleton) and `DatabaseInitializer.Initialize(connectionString)` that creates all 5 tables on startup (events, matches, competitors, brackets, bracket_matches). Arrays stored as JSON text columns. Updated `Program.cs` to read `ConnectionStrings:Default`, register `DatabaseConfig`, and call initializer before `app.Run()`. `verify.ps1 -Gate` exits 0.
Next iteration should do Task 3: Configure System.Text.Json for snake_case and define DTO/record types per specs/OVERVIEW.md.

## Iteration 3 — Task 3: Configure snake_case JSON and define DTOs
Added `ConfigureHttpJsonOptions` in Program.cs to set `JsonNamingPolicy.SnakeCaseLower` for both property names and dictionary keys. Created `src/EventManager.Api/Models.cs` with record types: `EventDto`, `CreateEventRequest`, `MatchDto`, `CreateMatchRequest`, `WeighInDto`, `CompetitorDto`, `CreateCompetitorRequest`, `BracketMatchDto`, `BracketDto`, `GenerateBracketRequest`. `verify.ps1 -Gate` exits 0.
Next iteration should do Task 4: Implement `GET /event/` and `GET /event/{id}/` endpoints reading from SQLite, returning JSON arrays.
