# Acceptance tests — the definition of done

**These acceptance tests are executed by `verify.ps1 -Accept` (its `Invoke-Test`
function).** In this project the human-authored `verify.ps1` *is* the acceptance test
runner: `Invoke-Test` starts the containerized server against a fresh, empty SQLite
database and drives every `GOAL.md` criterion through real HTTP calls. There is one
PowerShell test function per criterion. They are the authoritative, machine-checkable
"definition of done."

**Current state: RED.** No server project or container exists yet, so `Start-Server`
never reaches a listening server and every criterion fails (`verify.ps1 -Accept`
exits 1). This is the intended TDD starting point. The build loop turns them green one
implementation task at a time; when they are all green, `verify.ps1 -Accept` exits 0.

> There is intentionally **no second, parallel live-HTTP acceptance suite** here. The
> `verify.ps1` suite is order-dependent on a single shared container that starts empty
> (empty-list reads must precede any create); a second suite hitting the same container
> would create rows that break those empty-state assertions. See
> [specs/ASSUMPTIONS.md](../../specs/ASSUMPTIONS.md). Build agents may add their own
> unit/integration tests elsewhere under `tests/` (run via `dotnet test`), but the binding
> definition of done stays `verify.ps1 -Accept`.

## Invariant (never weaken these)

Per `CLAUDE.md`: never edit, delete, skip, or weaken the acceptance criteria — neither the
functions in `verify.ps1` (`Test-Ac1` … `Test-Ac14`, `Run-AcceptanceTests`, `Invoke-Test`)
nor this file's mapping. They encode the goal. Make the product satisfy them; do not bend
them to the product.

## Criterion → test mapping

Each `GOAL.md` acceptance criterion maps to a function in `verify.ps1`:

| GOAL crit | verify.ps1 function | What it asserts |
|-----------|---------------------|-----------------|
| 1  | `Test-Ac1`  | `GET /event/` on empty DB → 200 + empty list |
| 2  | `Test-Ac2`  | `GET /event/1/` → 200 + one event with all fields (`id`, `name`) |
| 3  | `Test-Ac3`  | `GET /match/` on empty DB → 200 + empty list |
| 4  | `Test-Ac4`  | `GET /match/1/` → 200 + one match with all fields (`id`, `type`) |
| 5  | `Test-Ac5`  | `GET /bracket/` on empty DB → 200 + empty list |
| 6  | `Test-Ac6`  | `GET /bracket/1/` → 200 + one bracket with per-match competitor groupings (`id` + `matches`/`groupings`) |
| 7  | `Test-Ac7`  | `GET /competitor/` on empty DB → 200 + empty list |
| 8  | `Test-Ac8`  | `GET /competitor/1/` → 200 + one competitor with all fields (`id`, `name`, `styles`, `birthdate`, `last_weigh_in`) |
| 9  | `Test-Ac9`  | `POST /create_event/` → 200 + new event id |
| 10 | `Test-Ac10` | `POST /create_event/` duplicate name → 500 + body contains `already exists` |
| 11 | `Test-Ac11` | `POST /create_match/` → 200 + match id and type (`kata`) |
| 12 | `Test-Ac12` | `POST /create_match/` type `BJJ` → 500 + body contains `invalid match type` |
| 13 | `Test-Ac13` | `POST /create_competitor/` → 200 + competitor data + new id (`Test comp 1`) |
| 14 | `Test-Ac14` | `POST /generate_bracket/` (event 1 + 3 matches + 8 competitors) → 200 + bracket data + new id |
| 16 | (exit code) | `verify.ps1 -Accept` exits 0 = build + lint + full acceptance suite all green |

Run order and fixture seeding (empty-state reads first, then create→read-back per entity,
then bracket) are handled by `Run-AcceptanceTests` in `verify.ps1`.

## How to run

```powershell
pwsh -File verify.ps1 -Accept              # full definition of done (quiet)
pwsh -File verify.ps1 -Accept -ShowTests   # per-criterion PASS/FAIL detail
```
