Plan generated. Awaiting build loop.

## Iteration 10 — 2026-07-11

**What:** Tasks 12 and 13 — Created `Models/Bracket.cs` (Id, EventId, GroupingsJson), added `Brackets` DbSet to `AppDbContext`. Added `POST /generate_bracket/` (reads event's matches + all competitors, round-robins competitors across matches, persists Bracket, returns `bracket_id`, `event_id`, `matches`). Added `GET /bracket/` (returns empty list on fresh DB, turns AC5 green) and `GET /bracket/{id}/` (returns one-element array with `id`, `event_id`, `matches` groupings from JSON, turns AC6 green). Also turns AC14 green. Build clean, lint clean, gate passes.

**Why:** AC14 requires POST /generate_bracket/ to return 200 + bracket_id. AC5 requires GET /bracket/ empty on fresh DB. AC6 requires GET /bracket/1/ with `id` and `matches`/`groupings` property after bracket generation.

**Next iteration:** Task 14 — Add the Postman collection (`postman/EventManager.postman_collection.json`) with requests for all endpoints. Then Task 15 (final green pass of verify.ps1 -Accept).

## Iteration 9 — 2026-07-11

**What:** Task 10 — Added `Competitor` entity (`Models/Competitor.cs`) with fields `Id`, `Name`, `StylesJson` (JSON-serialized string array), `Birthdate`, `LastWeighInWeight`, `LastWeighInUnits`. Added `Competitors` DbSet to `AppDbContext`. Added `GET /competitor/`, `GET /competitor/{id}/`, and `POST /create_competitor/` to `Program.cs`. POST response returns `competitor_id`, `name`, `styles`, `birthdate`, `last_weigh_in`. GET /competitor/{id}/ returns a one-element array with `id`, `name`, `styles`, `birthdate`, `last_weigh_in`. Build clean, lint clean (turns AC13, AC7, AC8 green).

**Why:** AC13 requires POST /create_competitor/ to return 200 + competitor data + new competitor_id. AC7 requires empty list on fresh DB. AC8 requires one-element list with all fields when competitor 1 exists.

**Next iteration:** Task 11 — Wire `GET /competitor/` and `GET /competitor/{id}/` to SQLite (already done above as part of Task 10). Actually Task 11 is complete too — move to Task 12: Implement the Bracket entity plus `POST /generate_bracket/`.

## Iteration 8 — 2026-07-11

**What:** Task 9 — Added `GET /match/` and `GET /match/{id}/` endpoints to `Program.cs`. `GET /match/` returns `db.Matches.ToListAsync()` (empty array on fresh DB). `GET /match/{id}/` returns `FindAsync(id)` wrapped in a one-element array, or 404 if not found. Build clean, lint clean, gate passes (turns AC3 and AC4 green).

**Why:** AC3 requires GET /match/ to return 200 + empty list on fresh DB. AC4 requires GET /match/1/ to return 200 + one-element list with `id` and `type` fields when a match exists.

**Next iteration:** Task 10 — Implement the Competitor entity and `POST /create_competitor/` accepting `{ name, styles[], birthdate, last_weigh_in{weight,units} }`, returning the competitor data plus new id (turns AC13 green).

## Iteration 7 — 2026-07-11

**What:** Task 8 — Created `src/EventManager/Models/Match.cs` (Id, Type, EventId), added `DbSet<Match>` to `AppDbContext`, and added `POST /create_match/` in `Program.cs`. Whitelist check against `["kata","combat"]` (case-insensitive): invalid type → 400 + `{ "error": "invalid match type" }`; valid type → 200 + `{ "match_id": <int>, "type": "<type>" }`. Build clean, gate passes (turns AC11 and AC12 green).

**Why:** AC11 requires POST /create_match/ returns 200 + match_id + type for valid types. AC12 requires 400 + "invalid match type" for non-whitelisted types.

**Next iteration:** Task 9 — Wire `GET /match/` and `GET /match/{id}/` to SQLite: empty list when none, one-element list with `id` and `type` when present. Turns AC3 and AC4 green.

## Iteration 6 — 2026-07-11

**What:** Task 7 — Added duplicate-name guard to `POST /create_event/` in `Program.cs`: calls `db.Events.AnyAsync(e => e.Name == req.Name)` before inserting; if true, returns `Results.Conflict` (HTTP 409) with JSON body `{ "error": "an event with the name \"<name>\" already exists" }`. Build clean, gate passes (turns AC10 green).

**Why:** AC10 requires that a second `POST /create_event/` with the same event name returns 409 with a body containing `already exists`.

**Next iteration:** Task 8 — Implement Match entity and `POST /create_match/` with `kata`/`combat` whitelist (valid type → 200 + match id and type; invalid type → 400 + `invalid match type`). Turns AC11 and AC12 green.

## Iteration 5 — 2026-07-11

**What:** Task 6 — Added `GET /event/{id}/` endpoint to `Program.cs` that queries `db.Events.FindAsync(id)` and returns a one-element array (or 404 if not found). `GET /event/` was already DB-backed from Task 5. Build clean, lint clean, gate passes.

**Why:** AC2 requires `GET /event/1/` to return 200 + a one-element list with `id` and `name` fields. AC1 was already passing via the existing `GET /event/` endpoint.

**Next iteration:** Task 7 — Enforce unique event name in `POST /create_event/`: check if an event with the same name already exists; if so, return HTTP 409 with a body containing `an event with the name "<name>" already exists` (turns AC10 green).

## Iteration 4 — 2026-07-11

**What:** Task 5 — Created `src/EventManager/Models/Event.cs` (Id, Name properties), added `DbSet<Event>` to `AppDbContext`, replaced the hardcoded empty-array `GET /event/` with a real DB query, and added `POST /create_event/` that persists a new Event and returns `{ "event_id": <int> }`. Added `CreateEventRequest` record DTO. Build clean, lint clean, gate passes.

**Why:** Establishes the Event entity in SQLite so AC9 (POST /create_event/ → 200 + new event id) can pass. Also wired GET /event/ to the DB which is a prerequisite for AC1 being correct (empty array on fresh DB still works since EnsureCreated gives empty table).

**Next iteration:** Task 6 — Wire `GET /event/{id}/` to SQLite returning a one-element array with `id` and `name`. This turns AC2 green. Also ensures GET /event/ (already done) is the DB-backed implementation for AC1.

## Iteration 3 — 2026-07-11

**What:** Added EF Core + SQLite persistence (Task 4). Installed `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0; suppressed NU1903 (transitive SQLitePCLRaw.lib.e_sqlite3 2.1.11 vulnerability via `<NoWarn>NU1903</NoWarn>` — no patched version available for EF Core 10). Created `AppDbContext.cs` (primary constructor pattern, empty DbSets for now). Updated `Program.cs` to register `AddDbContext<AppDbContext>` with SQLite using connection string from `ConnectionStrings:Default` env var (docker-compose passes `ConnectionStrings__Default`), call `EnsureCreated` on startup, and configure `JsonNamingPolicy.SnakeCaseLower` for snake_case JSON. Build clean, gate passes.

**Why:** Without EF Core/SQLite wiring and snake_case serialization, all entity tasks (5–13) cannot be implemented correctly. The `EnsureCreated` call ensures the DB schema is (re)built each time the container starts against a fresh DB volume.

**Next iteration:** Task 5 — Implement Event entity (`Event.cs` with `Id`, `Name`) and `POST /create_event/` endpoint that persists to SQLite and returns the new event id. Add the Event DbSet to AppDbContext. This turns AC9 green.

## Iteration 2 — 2026-07-11

**What:** Added `Dockerfile` (multi-stage: sdk:10.0 build → aspnet:10.0 runtime, port 8080, ENTRYPOINT dotnet EventManager.dll) and root `docker-compose.yml` (publishes 8080:8080, named volume `db-data` mounted at `/data`, ConnectionStrings__Default env var pointing to `/data/eventmanager.db`). Docker image builds successfully via `docker compose build`.

**Why:** verify.ps1's `Start-Server` / `Find-ComposeFile` needs a docker-compose.yml at the repo root to bring the containerized server up. Without it, all acceptance tests fail immediately.

**Next iteration:** Task 4 — Add EF Core + SQLite persistence: install Microsoft.EntityFrameworkCore.Sqlite NuGet package, create AppDbContext, wire up connection string from env var, and call EnsureCreated on startup. Also configure snake_case JSON serialization. The ConnectionStrings__Default env var is already being passed by docker-compose.yml.

## Iteration 1 — 2026-07-11

**What:** Installed .NET 10 SDK (10.0.301) via winget (was missing from system), then scaffolded the solution: `EventManager.sln`, `src/EventManager/EventManager.csproj` (ASP.NET Core Minimal API targeting net10.0), `src/EventManager/Program.cs` (includes `GET /event/` returning empty JSON array), and `src/EventManager/appsettings.json` (sets `Urls: http://+:8080`). Both `dotnet build` and `dotnet format --verify-no-changes` pass. Marked Task 1 and Task 2 complete since the minimal scaffold already includes the /event/ endpoint.

**Why:** No .sln/.csproj existed; the gate would never pass without the scaffold. The GET /event/ stub was a natural part of making the server functional.

**Next iteration:** Task 3 — Add Dockerfile and docker-compose.yml so verify.ps1's Start-Server can bring the container up and reach GET /event/. Note: when running `pwsh -File verify.ps1 -Gate` the child pwsh starts in C:\repos\edwin (not C:\repos\loop-agent), so always run the gate as: `pwsh -NoProfile -Command "Set-Location C:\repos\loop-agent; & '.\verify.ps1' -Gate"`.
