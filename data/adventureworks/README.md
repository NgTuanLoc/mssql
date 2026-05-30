# AdventureWorks Download Instructions

The AdventureWorks2022 backup file is not included in this repository (it is ~200 MB and gitignored).

## Download

1. Go to the Microsoft SQL Server Samples releases page:
   https://github.com/Microsoft/sql-server-samples/releases/tag/adventureworks

2. Download **`AdventureWorks2022.bak`** (the full OLTP database, ~200 MB).

## Install

3. Place the downloaded file in the `backups/` directory at the root of this repo:

   ```
   mssql/
   └── backups/
       └── AdventureWorks2022.bak   ← here
   ```

4. Run the restore script (container must already be running):

   ```powershell
   .\scripts\restore-adventureworks.ps1
   ```

5. Verify:

   ```powershell
   .\scripts\connect.ps1
   ```

   Then in the sqlcmd prompt:

   ```sql
   SELECT TOP 3 FirstName, LastName FROM AdventureWorks2022.Person.Person;
   GO
   ```

## Connection Details

| Field | Value |
|---|---|
| Server name | `localhost,1433` |
| Authentication | SQL Server Authentication |
| Login | `sa` |
| Password | value of `SA_PASSWORD` in `docker/.env` |

**SSMS / Azure Data Studio:** paste `localhost,1433` into the *Server name* field, choose *SQL Server Authentication*, enter `sa` and your password.

**ADO.NET connection string:**
```
Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=YourStr0ngP@ssword!;TrustServerCertificate=True;
```

**ODBC / DSN:**
```
Driver={ODBC Driver 18 for SQL Server};Server=localhost,1433;Database=AdventureWorks2022;Uid=sa;Pwd=YourStr0ngP@ssword!;TrustServerCertificate=yes;
```

> `TrustServerCertificate=True` is required because the container uses a self-signed certificate.

---

## Re-seeding

If you make changes you want to undo, run:

```powershell
.\scripts\reset-db.ps1
```

This drops and re-restores AdventureWorks (including any lesson schemas you created).
