# E10 — Modeling with the Fluent API

## What you'll learn

- What EF's conventions infer from plain classes, and how to read the schema they produce
- Keys: composite and alternate keys, and relationships that point at an alternate key
- Relationships and delete behaviors: `Cascade`, `Restrict`, `SetNull`, `ClientSetNull`, and the `ON DELETE` rule each one produces
- Owned types vs complex types for values without identity
- Value converters, shadow properties and backing fields
- Named query filters, new in EF Core 10

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — tables, keys, foreign keys and `ON DELETE` actions
- EF lessons [E07](../E07-change-tracking/README.md) (shadow values live in the change tracker) and [E08](../E08-savechanges-and-set-based-writes/README.md) (delete behaviors at save time)

## Setup

```
dotnet test --filter E10Examples --logger "console;verbosity=detailed"
```

**E10 is code-first.** The classes in `E10Model.cs` have no attributes. You write the mapping in each exercise's method, `protected override void Ex1(ModelBuilder model)`, exactly as you would in `OnModelCreating`. The check builds a model from your method, creates its tables in the **`EfLab_E10`** database (created on first use) inside the test's transaction, and inspects what came out:
- the schema, read from `sys.*` / `INFORMATION_SCHEMA`
- the model metadata
- how inserts and queries behave

The rollback removes the tables again, so every test starts with an empty database. AdventureWorks isn't involved, and the harness refuses to create or drop any database whose name doesn't start with `EfLab_`.

To see the DDL your configuration produces, print `db.Database.GenerateCreateScript()`.

## Concepts

### 1. Conventions

With nothing configured, EF still builds a full model:

- **Tables** are named after the entity type (`Book`) or its `DbSet`, and every reachable class becomes an entity type: `Author` is found through `Book.Author`.
- **Keys:** a property named `Id` or `<Type>Id`.
- **Types:** `string` becomes `nvarchar(max)`, `NOT NULL` when it's non-nullable (with nullable reference types on), and `decimal` becomes `decimal(18,2)` with a warning.
- **Relationships:** a navigation plus `<Navigation>Id` makes a foreign key. A missing FK property becomes a **shadow** FK (`MemberId`). A required relationship gets `ON DELETE CASCADE`. An optional one gets `ClientSetNull` (`NO ACTION` in the database).

The Fluent API (`modelBuilder.Entity<T>()...`) overrides any of it, and it wins over data annotations. Reading the generated DDL is the fastest way to see what the conventions decided.

Examples: `Conventions_build_a_model_from_the_classes`, `A_missing_foreign_key_property_becomes_a_shadow_property`.

### 2. Keys and relationships

- `HasKey(x => new { x.OrderId, x.LineNumber })` declares a composite key, in column order.
- `HasAlternateKey(...)`, or `HasPrincipalKey(...)` on a relationship, makes another unique column a key that foreign keys can reference.
- `HasOne(...).WithMany(...).HasForeignKey(...)` configures a relationship from the dependent side. `.IsRequired(false)` makes it optional.

| `DeleteBehavior` | Tracked dependents (EF, at `SaveChanges`) | `ON DELETE` in the schema |
|---|---|---|
| `Cascade` | deleted | `CASCADE` |
| `SetNull` | FK set to null | `SET NULL` |
| `ClientSetNull` (optional default) | FK set to null | `NO ACTION` |
| `Restrict` / `NoAction` / `ClientNoAction` | left alone; the delete fails if they still exist | `NO ACTION` |

The schema rule is what protects rows EF never loaded (E08).

Example: `Delete_behaviors_in_the_schema`.

### 3. Owned types vs complex types

Both map a class without its own table into its owner's columns (`ShippingAddress_City` by default; rename with `HasColumnName`).

| | `OwnsOne` (owned entity type) | `ComplexProperty` (complex type) |
|---|---|---|
| Identity | an entity type with a hidden key | none: a value, compared by value |
| Sharing one instance between two owners | fails: the instance belongs to one owner, so the save inserts NULLs for the other (`Cannot insert the value NULL into column 'ShippingAddress_Street'`) | works: both rows are saved with the values (copied per owner) |
| JSON mapping | `.ToJson()` | `.ToJson()` (EF 10) |

Use complex types for values such as an address or money, and owned types when you need entity-like behavior (E11 maps both to JSON).

Example: `Owned_type_vs_complex_type`.

### 4. Value converters

`HasConversion(...)` changes how a property is stored. `HasConversion<string>()` stores an enum's name. You can also pass your own conversion pair (`v => v.ToString(), s => Parse(s)`) or a `ValueConverter`. EF applies the converter to parameters and literals too, so `Status == Shipped` still translates (`[Status] = N'Shipped'`).

Example: `Value_converter_enum_as_string`.

### 5. Shadow properties and backing fields

- **Shadow property:** `Property<DateTime>("CreatedAt")` is in the model and the table but not on the class. Set it through `Entry(e).Property("CreatedAt")`, read it in queries with `EF.Property<DateTime>(e, "CreatedAt")`, and let the database fill it with `HasDefaultValueSql`.
- **Backing field:** EF can read and write a field instead of the property, which keeps setters private and invariants in methods. Fields named `_balance`, `balance` or `m_balance` are found by convention. Any other name needs `HasField("ledgerBalance")`. A read-only property with no backing field EF can find is not mapped at all.

Examples: `Shadow_properties_through_Entry_and_EF_Property`, `Conventional_backing_field_is_found`.

### 6. Named query filters (EF 10)

`HasQueryFilter("SoftDelete", t => !t.IsDeleted)` adds a predicate to every query of the entity type. Before EF 10 there was one filter per type, and `IgnoreQueryFilters()` removed it entirely. Named filters can be ignored one at a time with `IgnoreQueryFilters(["SoftDelete"])`, so a soft-delete rule and a tenant or archive rule stay independent.

Example: `Named_query_filters_in_SQL`.

## Pitfalls

- **Trusting conventions without reading the DDL.** Shadow FKs, `nvarchar(max)` and `NO ACTION` deletes are all easy to miss.
- **`ClientSetNull` when the database must null the FKs.** Use `SetNull` so rows EF never loaded are covered.
- **An owned type where a value would do.** Owned types carry identity rules complex types don't have.
- **A client-side default for an audit column.** `HasDefaultValue(DateTime.UtcNow)` is evaluated once, when the model is built. Use `HasDefaultValueSql`.
- **A read-only property you expect to be mapped.** Without a findable backing field, it silently isn't.

## Exercises

```
dotnet test --filter E10Exercises
```

Edit `Exercises.cs`. Each exercise is a model configuration: write it as you would in `OnModelCreating`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
