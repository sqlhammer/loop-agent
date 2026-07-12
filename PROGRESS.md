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

## Independent review (post-iteration 10)
Adversarial reviewer with fresh context audited the whole diff since startCommit
(044b6ed) against GOAL.md + specs/, and re-ran `verify.ps1 -Accept` independently:
build + lint + all 16 acceptance tests green, exit 0.

Checked for reward-hacking / gaps / regressions:
- Acceptance tests NOT weakened: the only change under `tests/acceptance/` is the
  removal of an unused `using System.Net.Http;` from ApiTestBase.cs — no assertion,
  scenario, or test was deleted/skipped/edited. All 16 still run and exercise real
  behavior. (Minor: touches a test-support file, but harmless to the definition of done.)
- No hardcoded/stubbed values: every endpoint is backed by real SQLite queries;
  expected values are computed from inserted data, not pinned to the tests.
- All 14 functional criteria + Docker/Postman deliverables map to genuine behavior.

FOUND ONE DEFECT (not covered by automated tests, but implied by GOAL):
- The Postman collection's `POST /generate_bracket/` example body sends
  `{event_id, match_ids, competitor_ids}` — it includes an ignored `match_ids`
  field and OMITS the required `match_type`. Executing it makes `match_type`
  deserialize to null; the subsequent `INSERT INTO bracket_matches` violates the
  `NOT NULL` constraint on `match_type` and throws an unhandled SqliteException →
  HTTP 500. GOAL requires the API be "exercisable end-to-end through a Postman
  collection" and the human checklist requires output that is "genuinely useful";
  a canned request that 500s violates both. Logged as Task 15.

REVIEW NOT PASSED — build loop should fix Task 15 before declaring done.

## Iteration 11 — Task 15: Fix Postman generate_bracket body
Changed the `POST /generate_bracket/` example body in `postman/EventManager.postman_collection.json` from `{"event_id":1,"match_ids":[1,2,3],"competitor_ids":[1,2,3,4,5,6,7,8]}` to `{"event_id":1,"competitor_ids":[1,2,3,4,5,6,7,8],"match_type":"combat"}` — drops the bogus `match_ids` field and adds the required `match_type` field that the API needs to avoid the NOT NULL constraint violation. `verify.ps1 -Accept` exits 0, all 16 tests still pass.
All tasks complete — no further work required.

## Independent review (post-iteration 11) — NOT PASSED
Fresh-context adversarial reviewer audited the full diff since startCommit (044b6ed)
against GOAL.md + specs/, re-ran `verify.ps1 -Accept` independently: 16/16 pass, exit 0.

Reward-hacking / gap / regression checks:
- Acceptance tests NOT weakened — only change under `tests/acceptance/` is removing an
  unused `using System.Net.Http;` from ApiTestBase.cs. All 16 tests still run and
  exercise real SQLite-backed behavior; expected values are computed from inserted
  data, never hardcoded to the tests.
- All 14 functional criteria + the Docker deliverable map to genuine implementation.

FOUND ONE DEFECT (false green) — logged as Task 16:
- The Postman collection deliverable is **gitignored and untracked in the repo**.
  `.gitignore:15` `postman/*` ignores it; the intended re-include on line 16
  `!postman\EventManager.postman_collection.json` uses a **backslash** separator, which
  git does not treat as a path separator (it's an escape), so the negation never matches
  `postman/EventManager.postman_collection.json`. Confirmed via
  `git check-ignore -v` (matches `.gitignore:15`) and `git ls-files postman/` (empty).
  `DeliverableTests` passes only because it reads the working-tree file from disk, not
  git — a fresh clone/`git clean` would have NO Postman collection, violating the GOAL
  deliverable "a postman collection for the various features." Verified that switching
  the separator to `/` un-ignores the file. Fix in Task 16.

REVIEW NOT PASSED — build loop should fix Task 16 before declaring done.

## New acceptance test added (post-iteration 12, outside the build loop)
Added `BracketTests.Generate_bracket_with_no_competitors_in_db_rejects_unknown_ids`: GIVEN
an empty database (no competitors exist), `POST /generate_bracket/` must not fabricate a
bracket from `competitor_ids` that don't reference real rows — it must return 400 with body
containing `unknown competitor id` and persist nothing. Documented as specs/ASSUMPTIONS.md
#14 and specs/OVERVIEW.md error-shapes/bracket-generation sections. Logged as
IMPLEMENTATION_PLAN.md Task 17 (currently `[ ]`, unimplemented).
`dotnet format EventManager.slnx --verify-no-changes` is clean. `dotnet test` shows 16
passed / 1 failed (the new test) — this is the expected RED state until Task 17 lands;
`BracketEndpoints.GenerateBracket` (src/EventManager.Api/BracketEndpoints.cs) still inserts
brackets/matches unconditionally without checking `competitors` table membership.
Next iteration should do Task 17: add the existence check and return 400 on unknown ids.

## Independent review (post-iteration 12) — PASSED
Fresh-context adversarial reviewer audited the full diff since startCommit (044b6ed)
against GOAL.md + specs/, and re-ran `verify.ps1 -Accept` independently: 16/16 pass, exit 0.

- Reward-hacking: NONE. Only change under `tests/acceptance/` is removing an unused
  `using System.Net.Http;` from ApiTestBase.cs. No test/assertion weakened, skipped, or
  deleted. All 16 tests run the real HTTP pipeline against a fresh temp SQLite DB;
  expected values computed from inserted data, never hardcoded.
- Gaps: NONE. Criteria 1–14 + 16 all map to genuine SQLite-backed behavior (real
  queries, real 409 on UNIQUE, real 400 on match-type whitelist, real bracket grouping).
- Prior findings genuinely fixed: Task 15 (Postman generate_bracket body now valid:
  event_id + competitor_ids + match_type, no match_ids) and Task 16 (.gitignore forward
  slash — `git check-ignore` reports nothing, `git ls-files` shows the collection tracked;
  a fresh clone ships the Postman deliverable).

Reviewer approved.

## Iter 13 — Task 17: Validate competitor IDs in POST /generate_bracket/
- Did: Added competitor-existence check in `BracketEndpoints.GenerateBracket` before any INSERT. Uses `SELECT COUNT(*) FROM competitors WHERE id IN (...)` with parameterized placeholders; if count < requested count, returns 400 with plain-text body `"unknown competitor id"` and persists nothing. All 17 acceptance tests pass (new test `Generate_bracket_with_no_competitors_in_db_rejects_unknown_ids` now green alongside all previous).
- Why: ASSUMPTIONS.md #14 — bracket generation must not fabricate rows from phantom competitor IDs that have no DB record.
- Next: All plan tasks are now complete (`[x]`). The supervisor should run `verify.ps1 -Accept` to confirm all 17 tests are green and declare the goal done.

## Independent review (post-iteration 13) — NOT PASSED
Fresh-context adversarial reviewer audited the full diff since startCommit (044b6ed) against
GOAL.md + specs/, and re-ran `verify.ps1 -Accept` independently: **17/17 pass, exit 0**.

Reward-hacking / gap / regression checks:
- Acceptance tests NOT weakened. Changes under `tests/acceptance/` are (a) removal of an
  unused `using System.Net.Http;` in ApiTestBase.cs and (b) an ADDED, stronger test
  (`Generate_bracket_with_no_competitors_in_db_rejects_unknown_ids`). Nothing skipped,
  deleted, or weakened. All tests run the real HTTP pipeline against a fresh temp SQLite DB.
- No hardcoded/stubbed values. Every endpoint (event/match/competitor/bracket, GET + POST)
  is backed by real parameterized SQLite queries; expected values are computed from inserted
  data, never pinned to the tests. Verified crit 9/10 (real 409 on UNIQUE), 11/12 (real 400
  on match-type whitelist), 13 (nested last_weigh_in round-trips), 14 (real bracket grouping),
  and the unknown-competitor-id 400 (real `SELECT COUNT(*) FROM competitors` membership check).
- Deliverables genuine: Dockerfile publishes EventManager.Api; Postman collection is now
  git-tracked (`git check-ignore` exits 1, `git ls-files postman/` lists it).

FOUND ONE DEFECT (false green) — logged as Task 18:
- A garbage-named file `"C\357\200\272reposEventManagerPROGRESS.md"` (mojibake of the Windows
  path `C:\repos\EventManager\PROGRESS.md`) was accidentally committed in iter 12 (commit
  60c1dfb) by a botched PROGRESS.md write and is TRACKED in the repo (`git ls-files` shows it).
  Its content duplicates the "Iteration 12 — Task 16" section already in the real PROGRESS.md,
  so nothing is lost — it is pure cruft. It breaks no acceptance test, but shipping the human a
  deliverable repo with a mojibake junk filename is a quality defect. Delete the tracked file.

REVIEW NOT PASSED — build loop should complete Task 18 (remove the stray file) before declaring done.

## Iter 14 — Task 18: Remove stray mojibake junk file
- Did: Confirmed `git ls-files` showed `"C\357\200\272reposEventManagerPROGRESS.md"` (mojibake of the Windows path `C:\repos\EventManager\PROGRESS.md`). Ran `git rm` using `printf` to construct the exact filename; working tree and index now clean. All 17 acceptance tests still pass; `verify.ps1 -Accept` exits 0.
- Why: The garbage-named file was committed by a botched PROGRESS.md write in iter 12 (commit 60c1dfb); its content duplicated the real PROGRESS.md section so no information was lost — it was pure cruft.
- Next: All plan tasks are now marked `[x]`. The supervisor should run `verify.ps1 -Accept` to confirm all 17 tests are green and declare the goal done.
