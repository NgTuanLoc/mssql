# Notebook Conversion Recipe (normative)

The validated transformation rules from the lesson 13 pilot (`docs/superpowers/specs/2026-07-21-notebook-lesson-format-design.md`, plan `2026-07-21-notebook-lesson-format.md`). Every rollout task applies these rules to one lesson directory `lessons/NN-topic/`. The pilot's three notebooks in `lessons/13-execution-plans-and-tuning/` are the reference implementation — match their JSON style exactly.

## R1 — File mapping

| Source (delete after conversion) | Target |
|---|---|
| `README.md` + `examples.sql` | `lesson.ipynb` |
| `exercises.sql` | `exercises.ipynb` |
| `exercises-solutions.sql` | `exercises-solutions.ipynb` |
| `setup.sql` | unchanged, never edited |

## R2 — Notebook JSON skeleton (exact)

- Top-level: `{"cells": [...], "metadata": {"kernelspec": {"name": "SQL", "display_name": "SQL", "language": "sql"}, "language_info": {"name": "sql", "version": ""}}, "nbformat": 4, "nbformat_minor": 2}`
- Markdown cell: `{"cell_type": "markdown", "metadata": {}, "source": "..."}`
- Code cell: `{"cell_type": "code", "metadata": {}, "source": "...", "outputs": [], "execution_count": null}`
- `source` is a single JSON string, LF (`\n`) line endings. No credentials or connection strings anywhere.

## R3 — Content transfer

SQL statements and README prose transfer **verbatim** — no rewritten queries, changed values, or reworded teaching content. The only permitted transformations:

- **R3a** File pointers rewritten: references to `README.md`/`examples.sql`/`exercises.sql`/`exercises-solutions.sql` become the corresponding notebook names ("Open `exercises.sql`" → "Open `exercises.ipynb`", etc.).
- **R3b** Tool guidance adapted, not dropped: where the README recommends SSMS or ADS for something, keep the guidance and add notebook context (pilot pattern: a `**Tooling note:**` in the intro cell saying to run cells in ADS attached to the `mssql-learn` connection, switching to SSMS where its features are needed).
- **R3c** A README "Worked Examples" list that is purely a table of contents for `examples.sql` is dropped (the examples are now inline).
- **R3d** SQL comments that ARE the explanation may move from code into the adjacent markdown cell (pilot: `-- Exercise N:` prompts became prompt prose; solution rationale comments became `**Why:**` cells). The words stay verbatim; only the container changes. Inline comments on code lines stay in the code.

## R4 — lesson.ipynb structure

1. Intro markdown cell: README title, "What you'll learn", setup note, tooling note (R3b).
2. Preamble code cell: `USE AdventureWorks2022;\nGO` plus whatever session settings the lesson's `examples.sql` preamble sets (`SET STATISTICS IO/TIME ON`, etc.).
3. README concept sections in original order as markdown cells; where a section corresponds to `-- Example N` blocks in `examples.sql`, the example code cells immediately follow it (short new intro line like `### Example N — <title from the source comment>` is permitted). Every README section and every `examples.sql` statement must appear, in order, exactly once.
4. Closing cells: any `SET ... OFF` teardown as a code cell; then Pitfalls/cheatsheet-link/exercises-pointer markdown (pointers per R3a).

## R5 — exercises.ipynb structure

Header markdown cell (title, "run setup.sql first, work through lesson.ipynb, solutions in exercises-solutions.ipynb", tool note if the source had one) → preamble code cell → per exercise/challenge: prompt markdown cell (from the `-- Exercise N:`/`-- Challenge N:` comment, as prose) + given/starter query code cells verbatim + answer placeholder (`-- Your ... here:` code cell; for prose answers a markdown cell `**Your explanation here:** _(edit this cell)_`) → teardown code cell if the source has one. **No solution content anywhere.**

## R6 — exercises-solutions.ipynb structure

Same header/preamble/teardown. Per exercise/challenge: prompt markdown cell **character-identical** to its `exercises.ipynb` counterpart → solution code cell(s) verbatim → rationale markdown cell (`**Why:** ...`) carrying the solution's explanatory comments (R3d).

## R7 — Batching

Each cell is one batch (ADS semantics). Statements that must be alone in a batch (`CREATE OR ALTER PROCEDURE/FUNCTION/VIEW/TRIGGER`, etc.) get their own cell with no other statements and no `GO`. `GO` inside a cell is allowed only where the source genuinely needs an intra-cell batch break (pilot: the `USE ... GO SET ...` preamble). This naturally fixes source files that were missing `GO` separators (known in lesson 10).

## R8 — Non-runnable content

- Statements **commented out** in the source stay non-runnable: put them in a markdown ```sql fence with the source's warning text (pilot: `DBCC FREEPROCCACHE`).
- Statements the source runs uncommented stay runnable code cells, even destructive ones (`DBCC DROPCLEANBUFFERS` in the buffer-pool lesson is intentional on a dev container).
- **Manual/multi-session blocks** (lessons 11, 17): where the source marks SQL as "run in a second session/window" or it would block/deadlock if executed sequentially in one session, make it a code cell but prefix the cell with the source's own session instruction comment, and EXCLUDE it from the harness run (R9) via the marker `-- [manual]` as the cell's first line (add the marker; it doubles as learner guidance — extend the comment, e.g. `-- [manual] Run this in a second ADS window/connection:`). Use it only where sequential execution is genuinely impossible; document each use in the report.

## R9 — Verification harness (run per notebook, from repo root)

```powershell
docker compose -f docker/docker-compose.yml up -d
$sa = ((Get-Content docker/.env) | Where-Object { $_ -match '^SA_PASSWORD=' }) -replace '^SA_PASSWORD=',''
Get-Content lessons/<NN-topic>/setup.sql -Raw |
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa" -No -b -I
$nb = Get-Content $nbPath -Raw | ConvertFrom-Json
$sql = ($nb.cells | Where-Object { $_.cell_type -eq 'code' -and -not (($_.source -join '').StartsWith('-- [manual]')) } |
        ForEach-Object { ($_.source -join '') + "`nGO" }) -join "`n"
$tmp = Join-Path $env:TEMP 'nb-verify.sql'
Set-Content -Path $tmp -Value $sql -Encoding UTF8
Get-Content $tmp -Raw |
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa" -No -b -I
if ($LASTEXITCODE -ne 0) { throw "notebook SQL failed" }
```

Note `-I` (QUOTED_IDENTIFIER ON): new versus the pilot harness — it matches how ADS/SSMS actually run cells and resolves the known Msg 1934 failures in lessons 09 and 12. Bash-tool alternative: prefix `docker exec` with `MSYS_NO_PATHCONV=1`; `SA_PASSWORD=$(sed -n 's/^SA_PASSWORD=//p' docker/.env | tr -d '\r')`.

Order: verify `lesson.ipynb`, then `exercises.ipynb`, then `exercises-solutions.ipynb` (one setup reset before each notebook's run is fine and preferred).

Empty-outputs check (must print 0):
```powershell
($nb.cells | Where-Object { $_.cell_type -eq 'code' -and $_.outputs.Count -ne 0 } | Measure-Object).Count
```

## R10 — Pre-existing bugs

If a source file fails the harness for a reason that predates conversion (the transfer is faithful but the SQL itself errors), apply the **minimal** fix that lets the lesson run, keep the teaching intent intact, and document it prominently in the report (precedent: pilot-era fixes for reserved keywords, missing GO). Never silently rewrite content to dodge an error. If the fix would change what the lesson teaches, STOP and report BLOCKED.

## R11 — Commit

One commit per lesson containing the three new notebooks AND the four `git rm`'d source files:
`feat: convert lesson NN to notebook format`
(conventional-commit prefix, no attribution footer). Commit only after all three notebooks pass R9.

## R12 — Self-review checklist (before reporting)

- Every source README section / SQL statement present exactly once, in order (or its omission justified by R3c/R8).
- Prompt cells identical across the two exercises notebooks (byte-compare).
- No solution leak into `exercises.ipynb`.
- Skeleton fields exact (R2); outputs empty; first code cell starts `USE AdventureWorks2022;`.
- `setup.sql` untouched; only the intended files in the commit.
