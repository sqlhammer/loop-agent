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

## Project conventions (EventManager)

- **Stack:** C# / .NET 10, ASP.NET Core Minimal APIs, SQLite. Product code lives in
  `src/EventManager.Api/`. The solution is `EventManager.slnx` (slnx format).
- **Test command:** `dotnet test EventManager.slnx` (what `verify.ps1`'s `Invoke-Test` runs).
  Acceptance tests are in `tests/acceptance/EventManager.Acceptance.Tests/`.
- **Lint:** `verify.ps1` runs `dotnet format EventManager.slnx --verify-no-changes`. ALWAYS
  run `dotnet format EventManager.slnx` as the last step of a task so lint stays green.
- **JSON contract is snake_case** (`id`, `match_type`, `start_date`, `last_weigh_in`, …).
  Routes include the trailing slash exactly as GOAL.md writes them (`/event/`, `/event/{id}/`).
- **By-id GETs return a JSON array** with one element (see specs/ASSUMPTIONS.md #2).
- **DB config:** read the connection string from configuration `ConnectionStrings:Default`
  (default `Data Source=eventmanager.db`) and create the schema on startup. Tests override
  this per test to point at a fresh empty temp database — never hard-code the DB path.
- Keep `Program.cs` exposing `public partial class Program {}` (the acceptance tests use
  `WebApplicationFactory<Program>`).
- Follow specs/OVERVIEW.md for object shapes, endpoints, and error bodies. Do exactly one
  IMPLEMENTATION_PLAN.md task per iteration.

