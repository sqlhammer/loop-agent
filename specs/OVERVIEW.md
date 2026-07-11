# Overview — Event Manager

A REST API for running martial-arts tournaments. It manages **events**, **matches**
(competitive contests such as katas and fights), **competitors**, and **brackets**
(competitor groupings generated for an event's matches). Data is persisted durably in
**SQLite**. The server runs inside a **Docker container** and exposes a public REST API.
A **Postman collection** exercises every endpoint.

## Tech stack

- **Language / runtime:** C# / .NET 10.
- **Web framework:** ASP.NET Core Minimal APIs (single Web API project).
- **Persistence:** Entity Framework Core with the SQLite provider. The database file
  lives on a Docker volume so data is durable across container process restarts.
- **Container:** one `Dockerfile` + one root `docker-compose.yml`. The service listens
  on port **8080** and publishes `8080:8080` (the verifier defaults to
  `http://localhost:8080`).
- **Client:** none beyond the Postman collection (a browser/GUI client is out of scope).

## Definition of done

`verify.ps1 -Accept` exits `0`. That command runs, in order:

1. `dotnet build` (must succeed),
2. `dotnet format --verify-no-changes` (code must already be formatted),
3. `Invoke-Test` — starts the container on a **fresh, empty SQLite DB**
   (`docker compose down -v` then `up -d --build`), drives every GOAL acceptance
   criterion through real HTTP calls in a fixed order, then tears the container down.

The acceptance tests themselves already exist and are authoritative — see
[tests/acceptance/README.md](../tests/acceptance/README.md).

## API surface

All paths use a trailing slash (ASP.NET Core routing matches trailing slashes by default).
Collection GETs return a JSON **array**; single-id GETs return a **one-element array**
(the verifier wraps a bare object into a one-element list, so returning the object alone
also passes — a one-element array is preferred for fidelity to the GOAL wording).
Business-rule violations return HTTP **500** with an error message in the body (the GOAL
specifies 500 for duplicate-event and invalid-match-type; we honor that literally).

| Method + path            | Behavior                                                                                 |
|--------------------------|------------------------------------------------------------------------------------------|
| `GET /event/`            | All events (empty `[]` when none).                                                       |
| `GET /event/{id}/`       | One-element list containing the event with all fields.                                   |
| `GET /match/`            | All matches (empty `[]` when none).                                                      |
| `GET /match/{id}/`       | One-element list containing the match with all fields.                                   |
| `GET /bracket/`          | All brackets (empty `[]` when none).                                                     |
| `GET /bracket/{id}/`     | One-element list containing the bracket, incl. competitor groupings per match.           |
| `GET /competitor/`       | All competitors (empty `[]` when none).                                                  |
| `GET /competitor/{id}/`  | One-element list containing the competitor with all fields.                              |
| `POST /create_event/`    | Create event; returns new event id. Duplicate name → 500 `... already exists`.           |
| `POST /create_match/`    | Create match; returns match id + type. Type not in whitelist → 500 `invalid match type`. |
| `POST /create_competitor/` | Create competitor; returns competitor data + new id.                                   |
| `POST /generate_bracket/`  | Generate a bracket for an event's matches/competitors; returns bracket data + new id.  |

## Data model

- **Event** — `id` (int, PK, auto), `name` (string, **unique**).
- **Match** — `id` (int, PK, auto), `event_id` (FK → Event), `type` (string, one of the
  whitelist `kata`, `combat`). Additional fields are allowed as long as `id` and `type`
  are always present in responses.
- **Competitor** — `id` (int, PK, auto), `name` (string), `styles` (list of strings, e.g.
  `["karate","BJJ"]`), `birthdate` (string, `MM-DD-YYYY`), `last_weigh_in`
  (`{ weight: number, units: string }`).
- **Bracket** — `id` (int, PK, auto), `event_id` (FK → Event), and **groupings**: the
  assignment of competitors to that event's matches. Serialized under a `matches` (or
  `groupings`) property so a `GET /bracket/{id}/` response shows which competitors are
  grouped into each match.

## Match-type whitelist

The allowed match types are configured as a whitelist: `kata`, `combat`. `create_match`
rejects anything else with 500 `invalid match type`.

## Bracket generation

`generate_bracket` takes an `event_id`, reads that event's matches and competitors, and
assigns competitors into the matches to form the bracket (a single-elimination-style
grouping of competitors per match is sufficient for the acceptance criterion). It persists
the bracket and returns its data plus the new bracket id.

## JSON conventions

Property names are `snake_case` to match the GOAL request/response examples
(`event_id`, `match_id`, `competitor_id`, `bracket_id`, `last_weigh_in`, `birthdate`).
Configure the JSON serializer accordingly (snake_case naming policy) or annotate DTOs.
