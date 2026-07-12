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

## Iteration 4 — Tasks 4 + 5: Event GET + POST endpoints
Created `src/EventManager.Api/EventEndpoints.cs` with `GET /event/`, `GET /event/{id}/`, and `POST /create_event/`. GET returns a JSON array (empty or with matching rows). POST inserts and returns the created EventDto with id; on SQLite UNIQUE constraint violation (error code 19), returns HTTP 409 as plain text (Results.Text) so the body matches the acceptance test's Contains check without JSON-encoded quotes. Wired `EventEndpoints.Map(app)` into Program.cs. All 4 EventTests pass; verify.ps1 -Gate is green.
Next iteration should do Task 6: Implement `GET /match/` and `GET /match/{id}/`.

## Iteration 5 — Tasks 6 + 7: Match GET + POST endpoints
Created `src/EventManager.Api/MatchEndpoints.cs` with `GET /match/`, `GET /match/{id}/`, and `POST /create_match/`. Combined both tasks because the acceptance test for crit #4 seeds via POST before GET (same coupling as events). POST validates match_type against {kata, combat} whitelist, returns 400 plain text "invalid match type" on invalid type. competitor_ids stored as JSON text in DB, deserialized on read. Wired `MatchEndpoints.Map(app)` in Program.cs. All 4 MatchTests pass; verify.ps1 -Gate is green.
Next iteration should do Task 8: Implement `GET /competitor/` and `GET /competitor/{id}/` with nested `last_weigh_in` object.

## Iteration 6 — Tasks 8 + 9: Competitor GET + POST endpoints
Created `src/EventManager.Api/CompetitorEndpoints.cs` with `GET /competitor/`, `GET /competitor/{id}/`, and `POST /create_competitor/`. Combined both tasks because the acceptance test for crit #8 seeds via POST before GET (same coupling as event/match pairs). Styles stored as JSON text in DB; last_weigh_in split into two flat columns (weigh_in_weight, weigh_in_units), reconstructed as nested WeighInDto on read. Wired `CompetitorEndpoints.Map(app)` in Program.cs. All 3 CompetitorTests pass; verify.ps1 -Gate is green.
Next iteration should do Task 10: Implement `POST /generate_bracket/` — persist a bracket for `event_id` whose matches partition `competitor_ids` into per-match groupings, returning 200 + bracket with new `id`.
