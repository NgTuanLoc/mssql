# E11 — Inheritance & JSON Columns

## What you'll learn

- The three ways to store a class hierarchy (TPH, TPT, TPC): the schema each produces, the SQL each generates, and what that costs
- Discriminators, and querying on them
- Mapping objects and lists to JSON columns with `ToJson()` (complex types in EF 10, or owned types)
- Querying into JSON (`JSON_VALUE`, `OPENJSON`) and updating inside it in place with `ExecuteUpdate` (`JSON_MODIFY`, new in EF 10)

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — tables, keys and constraints (the T-SQL track has no JSON lesson: this lesson introduces `JSON_VALUE`, `OPENJSON` and `JSON_MODIFY` through the SQL EF generates)
- EF lesson [E10](../E10-fluent-api-modeling/README.md) — complex types, owned types and shadow properties

## Setup

```
dotnet test --filter E11Examples --logger "console;verbosity=detailed"
```

Code-first, like E10. The classes are in `E11Model.cs`, and each model exercise is a configuration method that the check builds in the **`EfLab_E11`** database inside the test's transaction. Exercise 5 and the challenges are queries, run against the models defined at the top of `Checks.cs` (`OrderModel`, `PaymentsInOneTable`).

## Concepts

### 1. Table per hierarchy (TPH), the default

Map a hierarchy without choosing a strategy and EF uses TPH: one table for all the types, every subtype's columns included (nullable, because a card row has no `Iban`), and a **discriminator** column naming each row's type. By default the column is called `Discriminator` and holds the CLR type name, sized from those names. `HasDiscriminator<string>("Kind").HasValue<CardPayment>("card")...` renames it and picks the values. The discriminator is a shadow property, so queries can use it: `EF.Property<string>(p, "Kind")`.

Example: `Default_mapping_is_TPH`.

### 2. TPH vs TPT vs TPC

| | TPH | TPT (`UseTptMappingStrategy`) | TPC (`UseTpcMappingStrategy`) |
|---|---|---|---|
| Tables | one | one for the base, one per subtype | one per concrete type, none for an abstract base |
| Subtype columns | nullable, in the shared table | in the subtype's table | in the subtype's table, with the base columns repeated |
| Query one subtype | one table, `WHERE [Discriminator] = ...` | base `INNER JOIN` subtype | one table, no filter |
| Query the base type | one table | `LEFT JOIN` every subtype table | `UNION ALL` of every table |
| Insert one entity | one row | one row per level | one row |
| Keys | `IDENTITY` | `IDENTITY` on the base | a shared **sequence**, so keys stay unique across tables |

Rules of thumb from the EF docs: TPH is usually fastest and the default for good reason. TPC suits hierarchies where you mostly query one leaf type. TPT normalizes the schema but pays in joins on almost every query.

Examples: `One_subtype_query_in_each_strategy`, `Base_type_query_in_each_strategy`, `TPC_keys_come_from_a_sequence`.

### 3. JSON columns

`ToJson()` stores a whole object or list in one column: `nvarchar(max)` on SQL Server 2022 (SQL Server 2025 and Azure SQL can use the native `json` type).

```csharp
model.Entity<Order>(o =>
{
    o.ComplexProperty(x => x.Shipping, s => s.ToJson());   // one object
    o.ComplexCollection(x => x.Notes, n => n.ToJson());   // a list
});
```

EF 10 maps JSON through **complex types** (values, no keys). Owned types with `OwnsOne`/`OwnsMany(...).ToJson()` produce the same column, but they're entity types with hidden keys. Use JSON for data you read and write with its owner and rarely filter on. Separate tables are still better for data you join, index or update independently.

Example: `Owned_or_complex_both_store_JSON`.

### 4. Querying and updating JSON

EF translates paths into the JSON:

| LINQ | SQL |
|---|---|
| `o.Shipping.City == "Oslo"` | `JSON_VALUE([o].[Shipping], '$.City') = N'Oslo'` |
| `o.Notes.Any(n => n.Text.Contains(w))` | `EXISTS (SELECT 1 FROM OPENJSON([o].[Notes], '$') WITH (...) ...)` |
| `o.Notes.Count` | `(SELECT COUNT(*) FROM OPENJSON([o].[Notes], '$'))` |
| `ExecuteUpdate(s => s.SetProperty(o => o.Shipping.Carrier, c))` (EF 10) | `SET [Shipping] = JSON_MODIFY([Shipping], '$.Carrier', @p)` |

All of it runs in SQL, but none of it can seek an index. A `JSON_VALUE` filter scans every row unless you add a computed column on that path and index it.

Example: `Querying_into_JSON`.

## Pitfalls

- **TPT by habit.** Normalized tables feel right, and every query pays with joins.
- **TPC with `IDENTITY` in mind.** Keys come from a sequence, so don't rely on per-table identity values.
- **Filtering on a TPH subtype in C#.** `p is CardPayment` after `ToList()` loads every payment. Query `Set<CardPayment>()` or `OfType<CardPayment>()`.
- **JSON for data you search a lot.** `JSON_VALUE` filters can't use an index without a computed column.
- **Rewriting a whole JSON document to change one value.** In EF 10, `ExecuteUpdate` changes it in place.

## Exercises

```
dotnet test --filter E11Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
