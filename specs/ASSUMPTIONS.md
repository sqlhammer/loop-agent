# Assumptions

Ambiguities in GOAL.md resolved here rather than by asking a human. Each acceptance test
encodes the corresponding decision.

1. **Repository.** `C:\repos\EventManager` is already a git repository and is itself the
   project repo (working dir + branch `main`). We therefore do **not** run `git init` again
   (that would nest/corrupt the repo). The supervisor commits here, which satisfies "commits
   must be in the EventManager repo." No separate nested repo is created.

2. **Single-item GETs return a list.** Criteria 2/4/6/8 say the by-id endpoints return "a
   list with one X in it." Taken literally: `GET /{entity}/{id}/` returns a JSON **array**
   with a single element (not a bare object). Missing ids yield an empty array with 200.

3. **JSON is snake_case.** The only concrete request body in the GOAL (criterion 13) uses
   `last_weigh_in`, `weight`, `units`, `styles`. We standardize the entire contract on
   snake_case for consistency and to match that example exactly.

4. **`styles` is an array.** Criterion 13 shows `styles: {style:"karate", style:"BJJ"}` —
   invalid JSON (duplicate keys). Interpreted as the obvious intent: a string array
   `["karate","BJJ"]`.

5. **Trailing slashes are part of the route.** The GOAL writes every path with a trailing
   slash (`/event/`, `/event/1/`, `/create_event/`). Routes are defined with the trailing
   slash so requests match exactly as written.

6. **Deterministic ids start at 1.** Criteria reference specific ids ("event with id 1").
   On a fresh/empty database the first inserted row of each entity gets id 1 (SQLite
   AUTOINCREMENT / integer primary key). Acceptance tests create the row, then read it back
   by the id returned at creation, so they hold regardless but honor "id 1" on empty DBs.

7. **`create_event` / `create_match` take a JSON body.** Criteria 9 and 11 say
   "empty database ... POST /create_X/ returns 200 and the new id." A minimal valid body is
   supplied (an event needs a `name`; a match needs a `match_type`).

8. **`create_match` does not require an existing event.** Criterion 11 creates a match on an
   empty database, so `event_id` is optional/nullable at match creation.

9. **Match-type whitelist = {`kata`, `combat`}** (criterion 12). Any other value (e.g.
   `BJJ`) → 400 `invalid match type`. Comparison is treated as case-sensitive against the
   whitelist as written.

10. **`generate_bracket` input (revised).** It accepts only `event_id`; competitors and
    matches are looked up from the database rather than supplied by the caller. Every
    competitor currently in the database is placed into the bracket (paired up; a trailing
    odd competitor forms a group of one). Each generated match's `match_type` is taken from
    the event's own `matches` rows (cycled round-robin), so "three valid matches" in
    criterion 14's GIVEN state supplies the match types available to the bracket, not a cap
    on how many bracket matches are produced. The test asserts every competitor in the
    database appears in some grouping (encoding "groupings of competitors per match")
    without pinning a specific pairing algorithm.

11. **Criterion 16 is the aggregate gate.** "`verify.ps1 -Accept` exits 0" is satisfied by
    definition when build + lint + all other tests are green; it is not a self-referential
    unit test. It is verified by running `verify.ps1 -Accept` itself.

12. **Docker & Postman constraints are enforced by structural tests** (`DeliverableTests`):
    a `Dockerfile` that publishes `EventManager.Api`, and a Postman collection referencing
    every required endpoint. Building/running the container is not executed inside
    `dotnet test` (it would require the Docker daemon per test run); it is validated by the
    Dockerfile's presence/shape plus the human acceptance checklist.

13. **Response status is 200 (not 201) for creates**, exactly as the GOAL criteria state
    ("returns 200 and the ... id").

14. **`generate_bracket` rejects insufficient resources (revised).** GOAL crit #14 only
    covers the happy path (an event with matches and competitors already in the database).
    Since the request no longer supplies competitor ids or a match type, the endpoint must
    instead validate the three resources it looks up itself before creating anything,
    persisting no bracket or match rows on failure: `event_id` must reference an existing
    event (400 `unknown event id`), the event must have at least one match (400
    `insufficient matches`), and the database must have at least 2 competitors (400
    `insufficient competitors`).
