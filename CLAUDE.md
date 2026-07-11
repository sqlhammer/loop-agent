# Project rules for build agents

These rules are ALWAYS in effect for every iteration. The plan phase may extend this
file with project-specific conventions.

- Never edit, delete, skip, or weaken anything under `tests/acceptance/`. Those tests
  encode the goal and are the definition of done. You may add other tests.
- Do exactly one plan task per iteration. Keep changes small and reviewable.
- All durable knowledge goes in files (PROGRESS.md, IMPLEMENTATION_PLAN.md, specs/),
  never assume anything survives in context — the next iteration starts fresh.
- Prefer editing existing files over creating new ones. Match existing code style.
- The supervisor runs the verifier and git — you never commit.

## Project-specific conventions (Event Manager)

- **Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, EF Core + SQLite. Server listens on
  port **8080** inside a Docker container; the SQLite file lives on a named Docker volume.
- **The acceptance suite lives in `verify.ps1`'s `Invoke-Test`**, not only under
  `tests/acceptance/`. The "never weaken acceptance tests" rule covers `verify.ps1`'s
  `Test-Ac*`, `Run-AcceptanceTests`, and `Invoke-Test` functions AND
  `tests/acceptance/README.md`. Do not edit them to make the product pass — fix the product.
- **Definition of done:** `verify.ps1 -Accept` exits 0. It runs `dotnet build`,
  `dotnet format --verify-no-changes`, then the containerized REST acceptance suite.
- **Lint:** always run `dotnet format` before finishing a task; unformatted code fails the
  gate. Keep the build warning-clean.
- **Docker:** keep exactly one root `docker-compose.yml` publishing `8080:8080` so
  `verify.ps1`'s `Find-ComposeFile`/`Start-Server` can bring the stack up. The verifier
  wipes the DB volume each run (`down -v`); ensure schema is (re)created on startup.
- **JSON is snake_case** (`event_id`, `last_weigh_in`, `birthdate`, ...). Error responses
  for duplicate event and invalid match type must return HTTP **500** with the exact
  substrings `already exists` and `invalid match type` respectively.
- **Response shapes:** collection GETs return a JSON array; single-id GETs return a
  one-element array. First row of each entity against a fresh DB must have id `1`.
- See `specs/OVERVIEW.md` and `specs/ASSUMPTIONS.md` for the full contract.
