# Notebook Lesson Format — Design Spec

## Context

The curriculum's lesson convention (per `CLAUDE.md`) is five files per lesson: `README.md` (concepts), `setup.sql`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`. Learners read the README, then separately open and run the `.sql` files in SSMS or Azure Data Studio (ADS), switching back and forth between the explanation and the query window.

The goal of this change is to let learners see runnable T-SQL and its output right next to the explanation that motivates it, instead of reading a README and then running a separate script. Jupyter-style notebooks (`.ipynb`), opened in Azure Data Studio, provide that: markdown cells for explanation interleaved with executable SQL cells whose result grids render inline.

This spec covers the new per-lesson file convention and notebook structure, piloted on lesson 13 (execution-plans-and-tuning). The remaining 17 lessons are converted as separate follow-up work once the pilot is validated — this spec does not cover that rollout.

## New per-lesson file convention

Replaces the current five-file convention with four files:

| File | Role | Format |
|---|---|---|
| `setup.sql` | Idempotent schema/data prep for the lesson's `lessonNN` schema | Plain `.sql` (unchanged from today) |
| `lesson.ipynb` | Concepts + runnable examples, merged | Notebook: markdown + T-SQL cells |
| `exercises.ipynb` | Practice prompts with empty cells for the learner to fill in | Notebook |
| `exercises-solutions.ipynb` | Same prompts, each followed by a worked solution and a short rationale | Notebook |

`README.md` and `examples.sql` are retired as separate files — their content merges into `lesson.ipynb`. `exercises.sql` and `exercises-solutions.sql` become their notebook equivalents, keeping the same no-spoiler separation: a learner working through `exercises.ipynb` never sees the answer in the same file.

`setup.sql` is unchanged in format and execution: still run non-interactively via `sqlcmd` piped into the Docker container, as documented in `CLAUDE.md`. It carries no narrative content, so there's no benefit to notebook-izing it, and keeping it a script preserves the existing scripted verification flow untouched.

## Notebook runtime

Azure Data Studio's native SQL kernel is the target runtime — it runs `.ipynb` notebooks with T-SQL cells directly against a SQL Server connection, with no Python/ODBC layer needed. SSMS remains the recommended tool for moments that need its richer graphical execution-plan viewer (unchanged from today's guidance); ADS notebooks are for everything else, which is most of the content.

Each notebook's first code cell is `USE AdventureWorks2022; GO`, matching today's scripts. Cells may use `GO` as a batch separator exactly as the current `.sql` files do (e.g. around `CREATE PROCEDURE`).

Connecting the ADS notebook kernel to the Docker SQL Server instance (the "attach to connection" step) happens once per ADS session and is a prerequisite documented at the repo level, not embedded in individual notebooks — connection credentials must never be committed.

## `lesson.ipynb` structure

Direct translation of the current README + `examples.sql` pairing, preserving all existing content and ordering:

1. Markdown cell: lesson title + "What you'll learn" bullets (from the README's opening section).
2. Code cell: `USE AdventureWorks2022; GO`, plus `SET STATISTICS IO/TIME ON` where the lesson uses it.
3. For each concept section in the current README (e.g. "Reading plans", reference tables, rewrite patterns): a markdown cell with that explanation, immediately followed by the code cell(s) that run the matching example (today's `-- Example N:` comments become the runnable cells they annotate).
4. Concept sections with no runnable counterpart (pure reference tables, quick-reference lists) stay markdown-only — nothing is forced into a SQL cell that doesn't need to run.

This produces an "explain, then immediately run and see it" loop through the whole lesson, without changing what is taught or in what order.

**Handling tooling gaps (e.g. lesson 13's SSMS dependency):** where a step in the current README requires a tool ADS notebooks can't replicate — lesson 13's graphical plan viewer detail (operator costs, tooltips, memory grant warnings) is the known case — the markdown cell keeps that instruction verbatim, telling the learner to switch to SSMS for that specific step. The adjacent code cell still runs in the ADS notebook so the `STATISTICS IO`/`TIME` output and result sets remain visible inline. The gap is called out explicitly, never silently dropped.

## `exercises.ipynb` / `exercises-solutions.ipynb` structure

Both notebooks share the same setup preamble cell (`USE AdventureWorks2022; GO`, `SET STATISTICS IO/TIME ON`) as today's `exercises.sql`.

Per exercise, in both notebooks:

- **`exercises.ipynb`**: a markdown cell with the prompt (today's `-- Exercise N: ...` comment, written as prose) followed by an empty code cell for the learner's answer.
- **`exercises-solutions.ipynb`**: the identical markdown prompt cell, followed by a code cell with the worked solution, followed by a short markdown cell with the 1–2 line rationale that today lives as an inline `--` comment above the solution.

This preserves the current no-spoiler workflow exactly: a learner only ever opens `exercises.ipynb` while attempting the problems.

## Version control

Notebooks are committed with cell outputs cleared (`Clear All Outputs` in ADS before committing). This keeps diffs small and avoids baking stale or environment-specific result sets into the repo. Enforcement is manual discipline for this pilot, not a pre-commit hook — automated stripping (e.g. `nbstripout`) can be added later if manual clearing proves unreliable in practice.

## Pilot scope

Lesson 13 (execution-plans-and-tuning) is the pilot: it is already mid-edit, and it's the lesson most likely to expose format gaps (SSMS dependency, `GO`-batched procedure creation inside a notebook cell, `STATISTICS IO/TIME` message-output rendering in ADS's notebook UI). The pilot is considered validated once:

- `lesson.ipynb`, `exercises.ipynb`, and `exercises-solutions.ipynb` open cleanly in Azure Data Studio.
- All code cells execute successfully against the Dockerized AdventureWorks2022 instance, using `setup.sql` unchanged.
- Result grids and `STATISTICS IO`/`TIME` messages render as expected inline.

The remaining 17 lessons are **not** converted as part of this spec. Once the pilot is validated, the same mechanical pattern (README+examples → `lesson.ipynb`, exercises split → two notebooks, setup.sql untouched) is applied to them as separate follow-up work.

## Documentation updates

- `CLAUDE.md`'s "Lesson conventions" section is rewritten to describe the four-file convention and Azure Data Studio as the primary notebook runtime, with SSMS still called out for graphical-plan-viewer-dependent steps.
- A prerequisite note is added: Azure Data Studio installed, with a SQL Server connection to the Docker container configured once per environment.
- No changes to `docker/`, `scripts/win/`, `scripts/mac/`, or the `setup.sql` execution flow — all untouched by this change.

## Out of scope

- Converting lessons other than 13.
- Automated output-stripping tooling (nbstripout or similar) — deferred until/unless manual clearing proves insufficient.
- Any change to how `setup.sql` is authored, structured, or executed.
- Support for notebook runtimes other than Azure Data Studio (e.g. VS Code + mssql extension, Jupyter + Python kernel).
