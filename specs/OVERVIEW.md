# EventManager — Overview

## What is being built
A REST API for running **martial-arts tournaments**. The server persists data in SQLite,
ships as a Docker container, and is exercisable end-to-end through a Postman collection.
No client UI is in scope (Postman collection only).

## Stack
- **Language/runtime:** C# / .NET 10 (`net10.0`).
- **Web:** ASP.NET Core Minimal APIs (single `EventManager.Api` project).
- **Persistence:** SQLite. Connection string comes from configuration key
  `ConnectionStrings:Default` (env var `ConnectionStrings__Default`), defaulting to a local
  `eventmanager.db` file. The schema is created on startup so a fresh/empty database works.
- **Packaging:** `Dockerfile` at repo root builds and runs `EventManager.Api`.
- **API client artifact:** `postman/EventManager.postman_collection.json`.
- **Tests:** xUnit integration tests under `tests/acceptance/` using
  `WebApplicationFactory<Program>`; each test runs against its own fresh, empty temp SQLite
  database so IDs are deterministic (first insert => id 1).

## Solution layout
```
EventManager.slnx                                  # solution (slnx format)
src/EventManager.Api/                              # the API (build agents create this)
  EventManager.Api.csproj                          # Microsoft.NET.Sdk.Web
  Program.cs                                        # host + endpoint wiring; `public partial class Program`
  (models, data access, endpoint handlers)
tests/acceptance/EventManager.Acceptance.Tests/    # xUnit acceptance suite (DO NOT EDIT)
Dockerfile                                         # container image
postman/EventManager.postman_collection.json       # Postman collection
```

## JSON contract
The API speaks **snake_case** JSON (matching the GOAL's `create_competitor` example:
`last_weigh_in`, `weight`, `units`). Configure `System.Text.Json` with
`JsonNamingPolicy.SnakeCaseLower` (or explicit `[JsonPropertyName]`).

### Objects
- **event:** `id` (int), `name` (string, unique), `start_date` (string), `location` (string)
- **match:** `id` (int), `match_type` (string ∈ {`kata`,`combat`}), `name` (string),
  `event_id` (int?, nullable), `competitor_ids` (int[])
- **competitor:** `id` (int), `name` (string), `styles` (string[]), `birthdate` (string),
  `last_weigh_in` (object: `weight` (number), `units` (string))
- **bracket:** `id` (int), `event_id` (int), `matches` (array of
  `{ match_id, match_type, competitor_ids }` — the grouping of competitors per match)

## Endpoints (routes include the trailing slash exactly as the GOAL states)
| Method | Route | Behaviour |
|--------|-------|-----------|
| GET  | `/event/`           | 200 + array of all events (`[]` when empty) |
| GET  | `/event/{id}/`      | 200 + array containing the one matching event |
| GET  | `/match/`           | 200 + array of all matches |
| GET  | `/match/{id}/`      | 200 + array containing the one matching match |
| GET  | `/bracket/`         | 200 + array of all brackets |
| GET  | `/bracket/{id}/`    | 200 + array containing the one matching bracket (with groupings) |
| GET  | `/competitor/`      | 200 + array of all competitors |
| GET  | `/competitor/{id}/` | 200 + array containing the one matching competitor |
| POST | `/create_event/`      | 200 + created event (incl. `id`); **409** if name already exists |
| POST | `/create_match/`      | 200 + created match (incl. `id`, `match_type`); **400** if type not in whitelist |
| POST | `/create_competitor/` | 200 + created competitor (incl. `id`) |
| POST | `/generate_bracket/`  | 200 + created bracket (incl. `id`) with per-match competitor groupings |

### Error shapes
- Duplicate event name → **409** with body containing exactly:
  `an event with the name "Test Event 1" already exists` (the name is echoed).
- Invalid match type → **400** with body containing `invalid match type`.

## Bracket generation
`POST /generate_bracket/` accepts `{ "event_id", "competitor_ids": [...], "match_type" }`.
It creates and persists a bracket whose `matches` partition the supplied competitors into
per-match groupings (e.g. pairwise first-round matchups), and returns the bracket with its
new `id`. Every supplied competitor id must appear in exactly one grouping.

## Verification
`verify.ps1` drives the definition of done:
- **Build:** `dotnet build EventManager.slnx -c Release`
- **Lint:** `dotnet format EventManager.slnx --verify-no-changes`
- **Test:** `dotnet test EventManager.slnx -c Release` (discovers the acceptance suite)

`verify.ps1 -Accept` exiting 0 is acceptance criterion #16.
