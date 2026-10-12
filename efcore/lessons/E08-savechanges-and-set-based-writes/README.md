# E08 — SaveChanges & Set-Based Writes

## What you'll learn

- The SQL `SaveChanges` sends: batching, `MERGE ... OUTPUT` for inserts, and how generated keys come back
- Why tables with triggers need `HasTrigger` in the model, and the SQL EF uses instead of `OUTPUT`
- `ExecuteUpdate` / `ExecuteDelete`: one statement, nothing loaded, and what they bypass
- Delete behaviors: client-side cascade, `ClientSetNull`, and the database's own `ON DELETE CASCADE`
- Hooking into saves with a `SaveChangesInterceptor` (or the `SavingChanges` event) to stamp `ModifiedDate`

## Builds on

- T-SQL [lesson 08](../../../lessons/08-dml-insert-update-delete-merge/lesson.ipynb) — `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `OUTPUT`
- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — foreign keys and `ON DELETE` actions
- T-SQL [lesson 10](../../../lessons/10-views-procedures-functions/lesson.ipynb) — triggers
- EF lesson [E07](../E07-change-tracking/README.md) — entity states, which decide what `SaveChanges` writes

## Setup

```
dotnet test --filter E08Examples --logger "console;verbosity=detailed"
```

`E08Context.cs` is AdventureWorksContext with the `uSalesOrderHeader` trigger removed from the model. It's used by one example to show what happens without `HasTrigger`.

## Concepts

### 1. What SaveChanges sends

`SaveChanges` runs `DetectChanges`, then turns every Added, Modified and Deleted entry into SQL and sends it in as few round trips as it can (on SQL Server, batches of up to 42 statements by default). The batch is atomic: it runs in the current transaction if one is open (E09), otherwise EF starts its own when the save needs more than one statement (a single statement is atomic by itself).

| Change | SQL (no trigger on the table) |
|---|---|
| one insert | `INSERT ... OUTPUT INSERTED.[Id] VALUES (...)`: the identity comes back in the same statement |
| several inserts into one table | `MERGE ... USING (VALUES (...), (...)) ... OUTPUT INSERTED.[Id], i._Position`: one statement for all of them |
| update | `UPDATE ... SET <modified columns only> OUTPUT 1 WHERE [Key] = @p`: `OUTPUT 1` tells EF a row was updated |
| delete | `DELETE ... OUTPUT 1 WHERE [Key] = @p` |

Values the database generates (identity, `rowguid` and `ModifiedDate` defaults, computed columns like `SalesOrderNumber`) are read back and copied into your entities.

Example: `Inserts_are_batched`.

### 2. Triggers vs OUTPUT

SQL Server rejects an `OUTPUT` clause without `INTO` on a table that has an enabled trigger: "The target table ... cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause". So for tables declared with a trigger, EF switches strategy. The UPDATE has no `OUTPUT`, and a separate `SELECT ... WHERE @@ROWCOUNT = 1 AND [Key] = @p` reads the generated values back.

EF can only do that if it knows about the trigger. The scaffolder recorded every AdventureWorks trigger as `tb.HasTrigger("...")` (E01, challenge 1). In a code-first model, or a database whose triggers were added later, you declare them yourself. Forget one and every save to that table fails.

Examples: `Tables_with_triggers_get_no_OUTPUT_clause`, `Without_HasTrigger_the_update_fails`.

### 3. ExecuteUpdate and ExecuteDelete

```csharp
await db.Products.Where(p => p.ProductSubcategoryId == 1)
    .ExecuteUpdateAsync(set => set.SetProperty(p => p.ListPrice, p => p.ListPrice * 1.1m));
await db.SalesOrderHeaderSalesReasons.Where(r => r.SalesOrderId == 43697).ExecuteDeleteAsync();
```

Each is one set-based statement, sent immediately (not at `SaveChanges`), returning the number of rows affected. Nothing is loaded or tracked. They also **bypass** everything that lives in `SaveChanges`:

- the change tracker: entities already tracked keep their old values, and a re-query returns those same stale instances (identity resolution). Use `ChangeTracker.Clear()` or `Reload`.
- `SaveChanges` interceptors and the `SavingChanges` event: if the interceptor stamps `ModifiedDate`, set it in the statement yourself.
- concurrency tokens (E09) and client-side cascades.

Examples: `ExecuteUpdate_and_ExecuteDelete_are_single_statements`, `ExecuteUpdate_leaves_tracked_entities_stale`.

### 4. Deleting principals

What happens to the children when you delete a parent depends on two settings that are easy to confuse:

| Where | Setting | Effect |
|---|---|---|
| EF model | `DeleteBehavior.Cascade` | EF deletes **tracked** dependents itself (one `DELETE` each) |
| EF model | `DeleteBehavior.ClientSetNull` (scaffolded for `NO ACTION` keys) | EF sets the FK of **tracked** dependents to null |
| Database | `ON DELETE CASCADE` | the database deletes **all** dependents in the same statement |
| Database | `NO ACTION` | the DELETE fails while any dependent row still refers to the parent |

In AdventureWorks only two foreign keys cascade: order lines and order sales reasons to `SalesOrderHeader`. Everything else is `NO ACTION`. Deleting a subcategory therefore works only if no product refers to it any more. EF nulls the FKs of products it has loaded, but it can't see the ones it hasn't.

Examples: `Client_cascade_vs_database_cascade`, `ClientSetNull_nulls_tracked_dependents`.

### 5. Interceptors and events

A `SaveChangesInterceptor` (registered with `AddInterceptors(...)`, here through `NewContext<T>(interceptor)`) gets `SavingChanges` before the SQL is generated and `SavedChanges` / `SaveChangesFailed` after. Async saves call the `...Async` overrides **only**, so override both. At `SavingChanges` the states are final, which makes it the place for cross-cutting rules like stamping `ModifiedDate` or auditing. For one context instance, the `db.SavingChanges` event does the same with less ceremony.

Example: `SavingChanges_event_sees_the_final_states`.

## Pitfalls

- **`SaveChanges` in a loop.** One round trip per row. Add everything, save once.
- **A table with an undeclared trigger.** Every save to it fails. Declare `HasTrigger`.
- **Reading tracked entities after `ExecuteUpdate`.** They're stale until you clear or reload.
- **Relying on an interceptor for set-based writes.** `ExecuteUpdate` never calls it.
- **Deleting a parent whose children aren't loaded**, when the key is `NO ACTION`. FK violation. Null or delete the children first.
- **An interceptor that only overrides `SavingChanges`.** `SaveChangesAsync` skips it silently.

## Exercises

```
dotnet test --filter E08Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
