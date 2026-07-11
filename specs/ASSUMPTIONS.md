# Assumptions

Every ambiguity in `GOAL.md` was resolved as follows. Where the human-authored
`verify.ps1` acceptance suite pins down a concrete shape, the test wins over the prose.

## Where the acceptance tests live (most important)

`GOAL.md` criterion 16 makes `verify.ps1 -Accept` the definition of done, and the human
has **already authored the full acceptance suite inside `verify.ps1`'s `Invoke-Test`** —
one PowerShell function per criterion (`Test-Ac1` … `Test-Ac14`, plus AC16 being the exit
code itself), driving the real containerized REST API over HTTP. Those functions are the
authoritative, machine-checkable acceptance tests. They are **RED right now** (no server
project or container exists → `Start-Server` never reaches a listening server → every AC
fails; `verify.ps1 -Accept` exits 1, confirmed during planning).

We deliberately did **not** create a second, parallel executable acceptance suite under
`tests/acceptance/`, because:

1. The `verify.ps1` suite is intentionally **order-dependent on a single shared container
   started with an empty DB** (empty-list reads must run before any create). A second live
   suite hitting the same container would create rows that break the other suite's
   empty-state assertions — the suites would corrupt each other.
2. The goal centers on the **Docker + SQLite deployment artifact**. An in-process
   (`WebApplicationFactory`) mirror would test a different artifact and could pass while the
   real container fails — a weaker, misleading gate.

Instead, `tests/acceptance/` documents the authoritative suite and its
AC→function mapping, and pins the invariant that it must never be weakened. Build agents
**may** add their own unit/integration tests elsewhere under `tests/` (run with
`dotnet test`), but the binding definition of done remains `verify.ps1 -Accept`.

## Domain / API assumptions

- **Response shape for single-id GETs.** The GOAL says "a list with one X in it," and the
  verifier wraps a bare object into a one-element list. We return a **one-element array**
  from `GET /{entity}/{id}/`.
- **Error status codes.** Duplicate event name (crit 10) returns HTTP **409 Conflict**;
  invalid match type (crit 12) returns HTTP **400 Bad Request** — the conventional REST
  mapping for a resource conflict vs. a bad client input, respectively.
- **Error body format.** Duplicate event returns a body containing the substring
  `already exists` (verifier matches `already exists`); the fuller message is
  `an event with the name "<name>" already exists`. Invalid match type returns a body
  containing `invalid match type`.
- **`create_event` request body** is `{ "name": "<string>" }`. Response contains the new
  event id (bare int or `{ "event_id": ... }`/`{ "id": ... }` — the verifier accepts any).
- **`create_match` request body** is `{ "type": "<kata|combat>", "event_id": <int> }`.
  Response contains the match `id` and echoes the `type` string.
- **Match-type whitelist is `kata`, `combat`.** GOAL crit 12 names the whitelist
  `kata, combat` (the earlier prose "kata, combat" in crit 12 governs; `BJJ` is rejected).
- **`create_competitor` request body** matches the verifier's `New-CompetitorBody`:
  `{ name, styles: ["karate","BJJ"], birthdate: "09-01-2000", last_weigh_in: { weight: 160.4, units: "lbs" } }`.
  The GOAL's inline example uses a malformed `styles` object with duplicate keys; we treat
  `styles` as a **list of style strings** (as the verifier sends). `birthdate` is stored as
  the string `MM-DD-YYYY`; no date parsing/validation is required to pass.
- **Competitor response fields.** `GET /competitor/{id}/` must expose
  `id, name, styles, birthdate, last_weigh_in` (verifier checks these exact property names).
- **`generate_bracket` request body** is `{ "event_id": <int> }`. Preconditions for the
  test: event 1 exists with 3 matches and 8 competitors. It returns the bracket data plus a
  new `bracket_id`/`id`. `GET /bracket/{id}/` returns the bracket with a `matches` (or
  `groupings`) property describing competitor groupings per match.
- **Bracket algorithm.** Any deterministic assignment of the event's competitors into its
  matches that produces per-match competitor groupings is acceptable; a simple
  single-elimination pairing suffices. The goal does not require seeding/ranking logic.
- **IDs are 1-based auto-increment integers.** The verifier reads back `/event/1/`,
  `/match/1/`, `/competitor/1/`, `/bracket/1/`, so the first row of each entity must get
  id `1` against a fresh DB (EF Core SQLite autoincrement satisfies this).
- **Trailing slashes.** All routes carry a trailing slash; ASP.NET Core endpoint routing
  matches trailing slashes by default, so route templates can be written without them.
- **Port / base URL.** The service listens on `8080`; `docker-compose.yml` publishes
  `8080:8080`. The verifier defaults to `http://localhost:8080`.
- **Fresh DB per run.** `Invoke-Test` runs `docker compose down -v`, so the SQLite volume
  is wiped each run. Schema is (re)created on startup (EF `EnsureCreated` or migrations).
- **`Event Manager` is the product name**, not a required response literal — no test asserts
  that string appears in any payload.

## Constraint handling

- **Durability (SQLite).** Satisfied by an EF Core SQLite database file stored on a named
  Docker volume, so writes survive container process restarts. (The acceptance run wipes
  the volume between runs by design; durability is about not losing data mid-run.)
- **Lint.** `dotnet format --verify-no-changes` must pass, so all committed code must be
  formatted with `dotnet format` before the gate.
