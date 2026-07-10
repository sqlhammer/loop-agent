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
<!-- One or two sentences: what is being built. -->
A command-line TODO app that stores tasks in a local SQLite database.

## Stack & constraints
- Language/runtime: Node.js (TypeScript), no framework.
- Test runner: <the command verify.ps1's Invoke-Test runs, e.g. `npm test`>.
- Constraints: no network calls; all data in `./todo.db`.

## Acceptance criteria (each becomes an automated test — the definition of done)
1. GIVEN an empty database, WHEN I run `todo add "buy milk"`, THEN `todo list` prints a line containing `buy milk`.
2. GIVEN one task exists, WHEN I run `todo done 1`, THEN `todo list` shows it marked complete (e.g. `[x]`).
3. GIVEN a completed task, WHEN I run `todo clear`, THEN completed tasks are removed and `todo list` no longer shows them.
4. Running any command with no arguments prints usage help and exits with code 0.
5. `verify.ps1 -Accept` exits 0 (build + lint + full test suite all green).

## Out of scope (do NOT build)
- No due dates, no priorities, no multi-user support.

## Human acceptance checklist (I verify these by hand at the end — NOT automated)
- The CLI output is readable and the help text is genuinely useful.
