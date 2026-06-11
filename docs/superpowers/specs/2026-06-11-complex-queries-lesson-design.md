# Lesson 18 — Complex Queries: Design Spec

**Date:** 2026-06-11
**Author:** brainstorming session with Claude (superpowers:brainstorming)
**Status:** Approved by user

## Goal

Add a capstone lesson teaching how to **compose complex queries from scratch**: structuring
multi-step questions as CTE pipelines, using APPLY, PIVOT/UNPIVOT, and the classic query
patterns (top-N per group, gaps & islands, de-duplication, period comparisons). The learner
has finished lessons 06 (subqueries/CTEs) and 07 (window functions) and knows each technique
in isolation; this lesson teaches combining them.

## User Decisions (captured during brainstorm)

| Decision | Choice |
|---|---|
| Lesson focus | Writing/composing complex queries (not refactoring legacy ones, not performance) |
| Content | Full coverage: CTE pipelines, APPLY, PIVOT/UNPIVOT, classic patterns |
| Placement | Appended as `lessons/18-complex-queries/` — capstone, no renumbering |
| Implementation workflow | Use a git worktree for implementation |

## Non-Goals

- Refactoring/decomposing existing legacy queries (possible future lesson)
- Performance tuning of complex queries (covered by lessons 13 & 15)
- Dynamic SQL / dynamic pivoting

## Placement & Conventions

- Directory: `lessons/18-complex-queries/`
- Standard five files: `README.md`, `setup.sql`, `examples.sql`, `exercises.sql`,
  `exercises-solutions.sql`
- Idempotent `setup.sql` owning the `lesson18` schema (drop-and-recreate pattern)
- Prerequisites stated at the top of the README: lessons 06 and 07 required; lesson 13
  (plan reading) helpful but optional
- Root `README.md` curriculum table gains a row: `Capstone | 18 | Complex Queries`

## Teaching Spine: A Running Case Study

One realistic reporting request is built incrementally through the whole lesson, against
AdventureWorks Sales:

> "For each sales territory: monthly revenue, month-over-month growth, the top 3 products,
> and each product's contribution % to territory revenue."

Each concept section adds one layer to this query, so the learner watches a ~100-line query
grow from named steps instead of being confronted with it whole. The final assembled query
appears at the end of the README and in `examples.sql`.

## Concept Sections (README + examples.sql)

1. **Strategy first** — decompose the question into named steps before writing SQL; write
   the CTE skeleton (names + comments) first, fill in details after; debug by `SELECT`ing
   from intermediate CTEs. Includes a mermaid diagram of the case-study pipeline.
2. **Multi-CTE pipelines** — layered aggregation (aggregate of an aggregate), mixing window
   functions over grouped results, when to chain CTEs vs nest subqueries.
3. **CROSS / OUTER APPLY** — per-row subqueries ("lateral join" thinking), top-N per group,
   when APPLY beats JOIN, CROSS (inner semantics) vs OUTER (left semantics).
4. **PIVOT / UNPIVOT and conditional aggregation** — rows→columns and columns→rows; why
   `SUM(CASE WHEN …)` is often clearer and more flexible than PIVOT.
5. **Classic patterns** —
   - Top-N per group three ways: `ROW_NUMBER` + filter, `CROSS APPLY ... TOP`, `TOP WITH TIES`
   - De-duplication with `ROW_NUMBER` + `DELETE`
   - Gaps & islands (consecutive-run detection via row-number difference)
   - Running totals and period-over-period comparison (`LAG` + windowed `SUM`)

## setup.sql

Mostly relies on AdventureWorks directly. Creates two small `lesson18` tables where
AdventureWorks is too clean:

- `lesson18.CustomerStaging` — a copy of some customer/contact rows with **deliberate
  duplicates** (for the de-duplication pattern)
- `lesson18.GymVisit` — member-ID + visit-date rows with **gaps**, seeded
  deterministically (for gaps & islands)

## Pitfalls Section

- CTEs are not materialized — referencing the same CTE twice executes it twice
- `CROSS APPLY` silently drops rows when the inner side returns nothing (use `OUTER APPLY`)
- `ORDER BY` inside a CTE/subquery is meaningless (and illegal without TOP)
- Window functions can't appear in `WHERE` — wrap in a CTE and filter outside
- PIVOT requires an aggregate and exact column-value lists; conditional aggregation avoids both

## Exercises

About 8 exercises ramping in difficulty:

1–2. Rewrite nested subqueries as CTE pipelines (warm-up)
3–4. Top-N per group, once with ROW_NUMBER and once with APPLY
5. Conditional aggregation (pivot a result without PIVOT)
6. De-duplicate `lesson18.CustomerStaging`
7. Gaps & islands on `lesson18.GymVisit`
8. **Final boss:** a full pipeline mirroring the case study but on **Purchasing** data
   (vendors instead of territories), so it can't be copy-pasted from the examples

`exercises-solutions.sql` follows house style: one solution per exercise, 1–2 line comment
explaining the approach.

## Implementation Notes

- Implementation happens in a **git worktree** (user request)
- Every example and solution must be verified end-to-end against the Dockerized
  AdventureWorks2022 instance before commit (run `setup.sql`, then `examples.sql`, then
  `exercises-solutions.sql` through `sqlcmd`)
- Commit style: `feat: add lesson 18 - complex queries (CTE pipelines, APPLY, patterns)`
