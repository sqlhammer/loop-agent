# Implementation Plan

Ordered, one-iteration tasks that turn the RED acceptance suite GREEN. Do exactly one per
iteration. Acceptance tests under `tests/acceptance/` are the spec — never edit them.
Run `dotnet format EventManager.slnx` before finishing each task so the lint gate stays green.

- [x] Task 1: Scaffold `src/EventManager.Api` (`Microsoft.NET.Sdk.Web`, `net10.0`) with a `Program.cs` that builds and runs a host and exposes `public partial class Program`; confirm `verify.ps1 -Gate` (build + lint) exits 0.
- [x] Task 2: Add SQLite persistence wiring — read connection string from configuration `ConnectionStrings:Default` (default `Data Source=eventmanager.db`), open the connection, and create all tables (events, matches, competitors, brackets, bracket_matches) on startup if absent.
- [x] Task 3: Configure `System.Text.Json` for snake_case (`JsonNamingPolicy.SnakeCaseLower`) on all API responses/requests, and define the DTO/record types for event, match, competitor, and bracket per specs/OVERVIEW.md.
- [x] Task 4: Implement `GET /event/` and `GET /event/{id}/` (return a JSON array; empty array when none) — turns GOAL crit #1 and #2 green.
- [x] Task 5: Implement `POST /create_event/` returning 200 + created event with `id`, and 409 with body `an event with the name "<name>" already exists` on duplicate name — turns GOAL crit #9 and #10 green.
- [x] Task 6: Implement `GET /match/` and `GET /match/{id}/` returning the match with all data points (`id`, `match_type`, `name`, `event_id`, `competitor_ids`) — turns GOAL crit #3 and #4 green.
- [x] Task 7: Implement `POST /create_match/` returning 200 + `id` and `match_type`, and 400 `invalid match type` when `match_type` is not in {`kata`,`combat`} — turns GOAL crit #11 and #12 green.
- [x] Task 8: Implement `GET /competitor/` and `GET /competitor/{id}/` returning all data points including nested `last_weigh_in` — turns GOAL crit #7 and #8 green.
- [x] Task 9: Implement `POST /create_competitor/` accepting the GOAL body and returning 200 + competitor data with new `id` — turns GOAL crit #13 green.
- [x] Task 10: Implement `POST /generate_bracket/` — persist a bracket for `event_id` whose matches partition `competitor_ids` into per-match groupings, returning 200 + bracket with new `id` — turns GOAL crit #14 green.
- [x] Task 11: Implement `GET /bracket/` and `GET /bracket/{id}/` returning the bracket with `event_id` and per-match competitor groupings — turns GOAL crit #5 and #6 green.
- [x] Task 12: Add a repo-root `Dockerfile` (multi-stage: `dotnet publish` then a runtime image) with an `ENTRYPOINT` running `EventManager.Api` — turns the Dockerfile deliverable test green.
- [x] Task 13: Add `postman/EventManager.postman_collection.json` with a request for every required endpoint (the 4 GET families and the 4 POST creates) — turns the Postman deliverable test green.
- [x] Task 14: Run `dotnet format`, fix any remaining lint/build issues, and confirm `verify.ps1 -Accept` exits 0 (GOAL crit #16 — full suite green).
