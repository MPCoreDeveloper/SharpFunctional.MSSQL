# SharpFunctional.MSSQL — Tests

## Prerequisites

### SQL Server LocalDB

All integration tests require a running **SQL Server LocalDB** instance.  
LocalDB is installed automatically with Visual Studio (Data Storage and Processing workload) or can be installed separately via the [SQL Server Express LocalDB installer](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb).

### Connection String

Tests connect to the following instance:

```
Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=TestDB;Integrated Security=True;
Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;
Encrypt=True;TrustServerCertificate=False;Command Timeout=30
```

The `TestDB` database is **automatically created and recreated** by the test fixtures using EF Core `EnsureDeleted()` / `EnsureCreated()`.  
You do **not** need to create the database manually.

### Verify LocalDB is running

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

If the instance does not exist, create it:

```powershell
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

## Running Tests

```bash
dotnet test tests/SharpFunctional.MSSQL.Tests
```

Or use the Visual Studio Test Explorer.

## Test Framework

Tests use **xUnit v3** (`xunit.v3` 3.2.2).
