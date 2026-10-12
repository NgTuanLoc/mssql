# E07 — Change Tracking Internals

## What you'll learn

- The five entity states and what moves an entity between them
- How EF finds changes: snapshots and `DetectChanges`, and reading them with `Entry(...)` and `ChangeTracker.DebugView`
- Original vs current values, and why the UPDATE only writes modified columns
- Identity resolution: one instance per key per context, and the "already being tracked" error
- Disconnected entities: `Attach` vs `Update`, `CurrentValues.SetValues`, and updating without loading
- Graph pitfalls: what `Add` and `Update` do to every entity they reach, and `TrackGraph`

## Builds on

- No T-SQL lesson: change tracking is EF's own machinery.
- EF lessons [E05](../E05-loading-related-data/README.md) (loading graphs) and [E06](../E06-query-performance/README.md) (tracking vs no-tracking)

## Setup

```
dotnet test --filter E07Examples --logger "console;verbosity=detailed"
```

Everything a test writes is rolled back when it ends, so these lessons change AdventureWorks freely.

## Concepts

### 1. Entity states

| State | Meaning | `SaveChanges` sends |
|---|---|---|
| `Detached` | not tracked by this context | nothing |
| `Added` | new, not in the database yet | `INSERT` |
| `Unchanged` | tracked, same as when loaded (or last saved) | nothing |
| `Modified` | tracked, at least one property changed | `UPDATE` of the modified columns |
| `Deleted` | marked for deletion | `DELETE` |

A query with tracking returns `Unchanged` entities. `Add`, `Attach`, `Update` and `Remove` set states explicitly. After a successful `SaveChanges`, Added and Modified entities become Unchanged (with database-generated values such as identity keys copied back) and Deleted ones become Detached.

Example: `Entity_states_through_a_lifecycle`.

### 2. Snapshots and `DetectChanges`

Plain entity classes don't tell EF when you set a property. Instead, EF keeps a **snapshot** of the original values of every tracked entity and compares it with the current values in `DetectChanges`. That runs automatically before `SaveChanges`, in `ChangeTracker.Entries()` and `HasChanges()`, and for a single entity in `Entry(entity)`. A property set to the value it already had is not a change.

`ChangeTracker.AutoDetectChangesEnabled = false` turns the automatic calls off. That's useful when attaching thousands of entities in a loop, as long as you call `DetectChanges()` yourself before saving. `ChangeTracker.DebugView.LongView` (or `ShortView`) prints every tracked entity with its state, and for modified properties both values (`Name: 'X' Modified Originally 'Y'`).

Examples: `Changes_are_detected_against_a_snapshot`, `DebugView_shows_the_tracker`.

### 3. Identity resolution

A context tracks at most one instance per primary key. Query the same row twice and you get the same object, and the second query does **not** overwrite its values. That's how unsaved edits survive, and also why a re-query doesn't refresh stale data (use `Entry(e).ReloadAsync()`). Trying to track a second instance with the same key (`Attach`, `Update`, `Add`) throws "another instance with the same key value is already being tracked". Find the tracked one instead: `FindAsync` checks the tracker before querying, and `DbSet.Local` lists what's tracked.

Example: `Identity_resolution_one_instance_per_key`.

### 4. Original and current values

`db.Entry(e)` exposes `OriginalValues`, `CurrentValues`, and per property `.OriginalValue`, `.CurrentValue` and `.IsModified`. The UPDATE contains only properties marked modified. `CurrentValues.SetValues(obj)` copies matching properties from any object (a DTO or record), and only real changes become modified.

Example: `Only_modified_columns_are_updated`.

### 5. Disconnected entities: `Attach` vs `Update`

Entities that come from another context, an API request or a cache are not tracked. Two ways to bring one back:

| Method | State | UPDATE |
|---|---|---|
| `Attach(e)` | `Unchanged`; then change properties | only the properties you change afterwards |
| `Update(e)` | `Modified`, every property | every column, whatever its value |

`Update` is right when the object carries the complete new row. To change one column without reading the row, attach a stub that has only the key and set the property: one round trip, one column. EF can't tell you whether the row exists until `SaveChanges` updates 0 rows (E09).

Example: `Attach_vs_Update`.

### 6. Graphs

`Add`, `Attach` and `Update` act on everything reachable from the entity you pass, through loaded navigations:

- `Add(newProduct)` with `newProduct.ProductSubcategory = existingSubcategory` marks the existing subcategory **Added** too, and SaveChanges tries to insert it.
- `Update(order)` marks the order and every line Modified, even if one line changed. A line with no key value (an unset generated key) becomes Added.

Fixes: attach the existing entities first (`Attach` before `Add`), set foreign-key properties instead of navigations (`ProductSubcategoryId = 1`), or use `ChangeTracker.TrackGraph(root, node => ...)`, which visits every entity in the graph and lets you set each state, typically from `node.Entry.IsKeySet`.

Examples: `Add_on_a_graph_inserts_everything_it_reaches`, `TrackGraph_decides_per_entity`.

## Pitfalls

- **`Update` for a one-column change.** It rewrites every column, including ones another user changed in the meantime.
- **A re-query to "refresh".** Tracked entities are not overwritten. Use `Reload`.
- **`Entries().Any()` as "has changes".** It counts unchanged entities too. Use `HasChanges()`.
- **`Add` on a graph containing existing entities.** It re-inserts them.
- **Two instances, one key.** Find and update the tracked one.

## Exercises

```
dotnet test --filter E07Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
