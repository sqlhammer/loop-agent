# Implementation Plan

- [x] Task 1: Scaffold the solution and Web API project — create `EventManager.sln` and an ASP.NET Core (.NET 10) Minimal API project (e.g. `src/EventManager/EventManager.csproj`) that builds clean with `dotnet build` and `dotnet format --verify-no-changes`, and starts a Kestrel server listening on port 8080.
- [x] Task 2: Add `GET /event/` returning an empty JSON array from an in-memory list, so `dotnet build` passes and the endpoint responds (foundation for AC1).
- [x] Task 3: Add a `Dockerfile` and a root `docker-compose.yml` that build the project and publish `8080:8080`, so `verify.ps1`'s `Start-Server` can bring the container up and reach `GET /event/`.
- [ ] Task 4: Add EF Core + SQLite persistence — `AppDbContext`, connection string to a SQLite file stored on a named Docker volume, and schema creation on startup (EnsureCreated/migrations) against a fresh empty DB. Configure snake_case JSON serialization.
- [ ] Task 5: Implement the Event entity and `POST /create_event/` returning the new event id, backed by SQLite (turns AC9 green).
- [ ] Task 6: Wire `GET /event/` and `GET /event/{id}/` to SQLite — empty list when none, one-element list with `id` and `name` when present (turns AC1 and AC2 green).
- [ ] Task 7: Enforce unique event name in `POST /create_event/` — duplicate name returns HTTP 409 with a body containing `an event with the name "<name>" already exists` (turns AC10 green).
- [ ] Task 8: Implement the Match entity and `POST /create_match/` with the `kata`/`combat` whitelist — valid type returns 200 + match id and type; non-whitelisted type returns HTTP 400 with body `invalid match type` (turns AC11 and AC12 green).
- [ ] Task 9: Wire `GET /match/` and `GET /match/{id}/` to SQLite — empty list when none, one-element list with `id` and `type` when present (turns AC3 and AC4 green).
- [ ] Task 10: Implement the Competitor entity and `POST /create_competitor/` accepting `{ name, styles[], birthdate, last_weigh_in{weight,units} }`, returning the competitor data plus new id (turns AC13 green).
- [ ] Task 11: Wire `GET /competitor/` and `GET /competitor/{id}/` to SQLite — empty list when none, one-element list exposing `id, name, styles, birthdate, last_weigh_in` when present (turns AC7 and AC8 green).
- [ ] Task 12: Implement the Bracket entity plus `POST /generate_bracket/` — read an event's matches and competitors, assign competitors into matches to form per-match groupings, persist, and return the bracket data + new bracket id (turns AC14 green).
- [ ] Task 13: Wire `GET /bracket/` and `GET /bracket/{id}/` to SQLite — empty list when none, one-element list with `id` plus a `matches`/`groupings` property describing competitors per match (turns AC5 and AC6 green).
- [ ] Task 14: Add the Postman collection (`postman/EventManager.postman_collection.json`) with a request for every endpoint (the four GETs, single-id GETs, and four POSTs) using `{{baseUrl}}`.
- [ ] Task 15: Final green pass — run `dotnet format`, confirm `verify.ps1 -Accept` exits 0 with all acceptance criteria passing, and update `PROGRESS.md` (turns AC16 green).
