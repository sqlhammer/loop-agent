<!--
  GOAL.md — ONE OF THE TWO FILES YOU EDIT PER PROJECT. This is your entire input.

  Write a CLEAR, TESTABLE goal. The plan phase turns this into specs, an
  implementation plan, and acceptance tests (which you approve once). The build
  loop then runs unattended until every acceptance criterion below is provably met.

  Rules for a good goal:
  - Every criterion must be checkable by a machine (a test can pass/fail on it).
  - Prefer GIVEN / WHEN / THEN. Name concrete commands, inputs, and outputs.
  - State the tech stack and any hard constraints (language, deps, perf, style).
  - If a criterion can't be made into an automated test, it doesn't belong here —
    move it to your end-of-run human acceptance checklist instead.

  Delete this comment block and everything below, then write your goal.
-->

# Goal
An event management application that specialized in running martial arts touranments. It consists of a server running in a docker container with an exposed public API and a postman collection for the various features.

## Stack & constraints
- Language/runtime: C#, .NET 10
- Build the project in directory: C:\repos\EventManager
  - init a git repo for that directory before adding any files
  - All commits must be in the EventManager repo, not the loop-agent repo
- Test runner: <the command verify.ps1's Invoke-Test runs, e.g. `npm test`>.
- Constraints: 
  - Event data is durable using SQLite
  - The application server runs on a docker container
  - The application is accessible via REST API calls in a Postman collection
  - Required API endpoints
    - GET event <all events or a single one by id>
    - GET match <competitive matches such as katas and fights>
    - GET bracket <all brackets for a given event id or a single bracket with event id and bracket id or match id>
    - GET competitor <all competitors or a single one by id>
    - POST create_event
    - POST create_match
    - POST create_competitor
    - POST generate_bracket

## Acceptance criteria (each becomes an automated test — the definition of done)
1. GIVEN an empty database, WHEN I run `GET /event/`, THEN `Event Manager` returns 200 and an empty list of events.
2. GIVEN a database containing an event with id 1, WHEN I run `GET /event/1/`, THEN `Event Manager` returns 200 and a list with one event in it and the response contains all of the event object data points.
3. GIVEN an empty database, WHEN I run `GET /match/`, THEN `Event Manager` returns 200 and an empty list of matches.
4. GIVEN a database containing a valid match with id 1, WHEN I run `GET /match/1/`, THEN `Event Manager` returns 200 and a list with one match in it and the response contains all of the match object data points.
5. GIVEN an empty database, WHEN I run `GET /bracket/`, THEN `Event Manager` returns 200 and an empty list of brackets.
6. GIVEN a database containing a valid bracket, WHEN I run `GET /bracket/1/`, THEN `Event Manager` returns 200 and a list with one bracket in it and the response contains all of the bracket object data points including the groupings of competitors per match.
7. GIVEN an empty database, WHEN I run `GET /competitor/`, THEN `Event Manager` returns 200 and an empty list of competitors.
8. GIVEN a database containing a valid competitor, WHEN I run `GET /competitor/1/`, THEN `Event Manager` returns 200 and a list with one competitor in it and the response contains all of the competitor object data points.
9. GIVEN an empty database, WHEN I run `POST /create_event/`, THEN `Event Manager` returns 200 and the event id of the event that was just created.
10. GIVEN a database containing an event named `Test Event 1`, WHEN I run `POST /create_event/` with a request body that contains a new event named `Test Event 1`, THEN `Event Manager` returns 409 and an error response that `an event with the name "Test Event 1" already exists`.
11. GIVEN an empty database, WHEN I run `POST /create_match/`, THEN `Event Manager` returns 200 and the match id and match type of the match that was just created.
12. GIVEN a whitelist of match types `kata, combat`, WHEN I run `POST /create_match/` with a request body that contains a new match of type `BJJ`, THEN `Event Manager` returns 400 and an error response `invalid match type`.
13. GIVEN an empty database, WHEN I run `POST /create_competitor/` with `{name: "Test comp 1",styles: {style: "karate",style: "BJJ"},birthdate:"09-01-2000",last_weigh_in:{weight: 160.4, units:lbs}}`, THEN `Event Manager` returns 200 and the competitor data along with its newly generated competitor id that was just created.
14. GIVEN a database containing a valid event with id 1, a three valid matches, and eight valid unique competitors, WHEN I run `POST /generate_bracket/`, THEN `Event Manager` returns 200 and the bracket data along with its newly generated bracket id that was just created.
16. `verify.ps1 -Accept` exits 0 (build + lint + full test suite all green).

## Out of scope (do NOT build)
- client application, other than the postman collection

## Human acceptance checklist (I verify these by hand at the end — NOT automated)
- The API output is readable and the response text is genuinely useful.
