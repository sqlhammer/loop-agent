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

## Iteration 7 — Tasks 10 + 11: Bracket GET + POST endpoints
Created `src/EventManager.Api/BracketEndpoints.cs` with `GET /bracket/`, `GET /bracket/{id}/`, and `POST /generate_bracket/`. Combined both tasks because GET /bracket/{id}/ test seeds via POST /generate_bracket/ (same coupling pattern as prior endpoint pairs). POST partitions competitor_ids into pairs (2 per match; last group has 1 if odd), inserts a bracket row then bracket_match rows storing competitor_ids as JSON text, returns BracketDto. GET endpoints query brackets + bracket_matches and reconstruct BracketDto with nested BracketMatchDto array. Wired `BracketEndpoints.Map(app)` in Program.cs. All 3 BracketTests pass (crits 5, 6, 14); verify.ps1 -Gate is green.
Next iteration should do Task 12: Add a repo-root Dockerfile (multi-stage build) with ENTRYPOINT running EventManager.Api.

## Iteration 8 — Task 12: Add Dockerfile
Created `Dockerfile` at repo root with a multi-stage build: SDK image runs `dotnet publish src/EventManager.Api/`, aspnet runtime image copies the publish output and sets `ENTRYPOINT ["dotnet", "EventManager.Api.dll"]`. The DeliverableTests.Dockerfile_exists_and_publishes_the_api test checks that the file exists, contains "EventManager.Api", and contains "ENTRYPOINT" — all satisfied. `verify.ps1 -Gate` exits 0.
Next iteration should do Task 13: Add `postman/EventManager.postman_collection.json` covering all 8 required endpoints — turns the Postman deliverable test green.

## Iteration 9 — Task 13: Add Postman collection
Created `postman/EventManager.postman_collection.json` — a valid Postman v2.1 collection with 12 requests covering all 8 required endpoint families (GET /event, GET /match, GET /bracket, GET /competitor, POST /create_event, POST /create_match, POST /create_competitor, POST /generate_bracket). The DeliverableTests.Postman_collection_exists_and_covers_every_required_endpoint test checks file existence, valid JSON, and presence of all 8 endpoint strings — all pass. `verify.ps1 -Gate` exits 0.
Next iteration should do Task 14: Run `dotnet format`, fix any remaining lint/build issues, and confirm `verify.ps1 -Accept` exits 0 (full acceptance suite green).

## Iteration 10 — Task 14: Full acceptance suite green
Ran `dotnet format EventManager.slnx` (no changes needed) and `verify.ps1 -Accept` — all 16 acceptance tests passed (Failed: 0, Passed: 16). Build, lint, and full test suite are all green. All tasks in IMPLEMENTATION_PLAN.md are complete. The project is done.
No further tasks remain.
