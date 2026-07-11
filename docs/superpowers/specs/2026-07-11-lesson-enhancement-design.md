# Lesson Enhancement Design — Deep READMEs + Challenge Exercises

**Date:** 2026-07-11
**Status:** Approved
**Scope:** All 18 lessons (`lessons/01` … `lessons/18`)

## Goal

Bring every lesson up to the quality bar set by lesson 18 (the capstone): READMEs
that explain *how to think* about a topic — not just its syntax — and exercise sets
that include realistic, multi-concept challenge problems. Nothing existing is
deleted or renumbered; content is expanded in place.

## Motivation

The current lessons are correct but thin. READMEs run 50–100 lines with no sample
output, no mental-model section, and pitfalls stated as one-line warnings. Exercise
sets are ~5 short single-concept problems. Lesson 18 shows what the curriculum can
be: a motivating case study, a decomposition diagram, stepwise concept build-up,
and exercises that require combining techniques. This work applies that standard
everywhere.

## Design

### 1. Enhanced README template

Every lesson README is reworked to this structure, targeting 150–250 lines scaled
to topic weight (lesson 01 lighter, lesson 13 heavier). Existing accurate content
is kept and expanded — this is a rework, not a from-scratch rewrite.

```markdown
# Lesson NN — Topic

**Prerequisites:** lessons it builds on, one line.

2–3 sentence motivating hook: the real-world problem this topic solves,
phrased as something a developer actually hits.

## What you'll learn
(bullets — kept from current READMEs, tightened)

## The mental model
The one diagram or analogy explaining HOW it works, not just syntax.
Mermaid for flows/structures; ASCII tables where layout matters
(index pages, buffer pool).

## Setup
(unchanged convention)

## Concepts
### 1. Concept name
Stepwise: minimal runnable snippet → sample output inline as a small
fenced block → one paragraph of why/when. Each concept ends with
"when to reach for this" guidance.

## Worked examples
Maps to examples.sql; each example annotated with what to notice
in its output.

## Pitfalls
Each pitfall gets a short "see it break" snippet where practical.

## Exercises
Points to exercises.sql; describes the CHALLENGE section and sets
expectations (challenges may combine concepts across lessons).

## Cheatsheet link
(unchanged)
```

Key upgrades over current READMEs: inline sample outputs (currently absent
everywhere), the mental-model section (currently only lesson 18 has one),
pitfalls with demos, and why/when framing per concept.

### 2. Challenge exercises

Each lesson's `exercises.sql` keeps its existing exercises untouched and gains a
clearly marked section:

```sql
------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: <multi-step business question, stated the way a
--              stakeholder would ask it — the decomposition is
--              part of the exercise>
-- Expected columns: ...
-- Hint (optional, only where the leap is big): ...
-- Your query here:
```

**Count:** 2–4 challenges per lesson.

**Difficulty philosophy:** difficulty comes from combination and realism, not
obscure syntax. Examples of the intended register:

- Lesson 07: "find each salesperson's longest streak of consecutive months with
  rising sales" (windows + gaps-and-islands thinking)
- Lesson 12: "here's a workload of 3 queries — design the minimal index set and
  justify each column's position"
- Lesson 17: "reproduce this deadlock from two sessions, then fix it two
  different ways"

Internals lessons (14–17) get investigation-style challenges using DMVs rather
than pure query-writing.

### 3. Solution style

In `exercises-solutions.sql`:

- **Regular exercises:** keep the existing 1–2 line comment convention. No churn.
- **Challenges:** a fuller *reasoning path* comment (3–6 lines: how to decompose
  the problem, which technique and why, what a wrong-but-tempting approach would
  be), then the query.

### 4. Spec addendum

The five-file-per-lesson convention holds. The master design spec
(`2026-05-29-mssql-learning-design.md`) gets a short addendum documenting the
challenge-section and challenge-solution conventions so future lessons follow them.

### 5. Cleanup

Delete the empty, untracked leftover directory `lessons/18-diagnosing-common-issues/`.

## Workflow

### Rollout order (Approach A: pilot, then tiers)

1. **Pilot: lesson 07** (window functions) — full treatment, then a user review
   gate on style/depth before anything else is touched.
2. After pilot approval: tiers in order — 01–04, 05–08, 09–11, 12–13, 14–17, 18.
3. Lesson 18 gets the lightest touch (already the exemplar): challenge-solution
   comment upgrades and small README polish only.
4. Between tiers: short progress summary to the user, no review gate — the pilot
   review is the style gate.

### Verification (per lesson, before its commit)

Container: `mssql-learn` (MSSQL 2022 + AdventureWorks2022), run via
`docker exec … sqlcmd … -No` per CLAUDE.md.

1. Run the lesson's `setup.sql` (confirms idempotency if setup changed).
2. Run every new/changed README snippet, and `examples.sql` if touched.
3. Run the full `exercises-solutions.sql` — every solution, old and new, must
   execute and return plausible results, spot-checked against the exercise's
   stated expected columns.
4. Two-session challenges (blocking/deadlocks in 14–17) verified with two
   parallel `sqlcmd` invocations.

### Commits

- One conventional commit per lesson:
  `feat: enhance lesson NN - deep README + challenge exercises`
- One `docs:` commit for the spec addendum.
- One `chore:` commit deleting `lessons/18-diagnosing-common-issues/`.

## Out of scope

- Cheatsheets, helper scripts, Docker setup.
- Renumbering, deleting, or rewriting existing exercises — existing exercise
  content is preserved verbatim; only new material is added around it.
- New lessons (the leftover empty directory is deleted, not built out).
