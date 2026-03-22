# SharpFunctional.MSSQL

<p align="center">
  <img src="sharpfunctional_mssql_logo_512.png" alt="SharpFunctional.MSSQL" width="120" />
</p>

[![NuGet](https://img.shields.io/nuget/v/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![CI](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml)
[![NuGet Publish](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)
[![NuGet](https://img.shields.io/badge/NuGet-1.0.0-blue.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![Tests](https://img.shields.io/badge/Tests-57-brightgreen.svg)](#testing)
[![C#](https://img.shields.io/badge/C%23-14-purple.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)

[English](README.md) | [Nederlands](docs/README.nl.md)

Functional-first SQL Server access for modern .NET.

`SharpFunctional.MSSQL` is a `.NET 10` / `C# 14` library that combines:
- **Entity Framework Core** convenience
- **Dapper** performance
- **LanguageExt** result types (`Option<T>`, `Seq<T>`, `Fin<T>`)
- **No-exception API surface** for expected failure paths

---

## Why SharpFunctional.MSSQL?

This package helps you build SQL Server data access with:
- explicit success/failure flows
- composable async operations
- transaction-safe execution
- structured logging
- built-in retry/timeout configuration
- OpenTelemetry tracing hooks

---

## Features

### Functional API model
- `Option<T>` for optional values
- `Seq<T>` for query result sequences
- `Fin<T>` for success/failure with error context

### EF Core integration (`EfFunctionalDb`)
- `GetByIdAsync<T, TId>`
- `FindOneAsync<T>`
- `QueryAsync<T>`
- `AddAsync<T>`
- `SaveAsync<T>`
- `DeleteByIdAsync<T, TId>`
- `CountAsync<T>`
- `AnyAsync<T>`
- explicit `WithTracking()` mode

### Dapper integration (`DapperFunctionalDb`)
- `QueryAsync<T>`
- `QuerySingleAsync<T>`
- `ExecuteStoredProcAsync<T>`
- `ExecuteStoredProcSingleAsync<T>`
- `ExecuteStoredProcNonQueryAsync`

### Transaction support (`FunctionalMsSqlDb`)
- `InTransactionAsync<T>` for commit/rollback flows
- `InTransactionMapAsync<TIn, TOut>` for transactional mapping
- support for EF and Dapper backends

### Async + cancellation first
- CancellationToken support on all async public APIs
- cancel propagation through EF, Dapper, retries, and helper extensions

### Resilience options
- `SqlExecutionOptions` for:
  - command timeout
  - max retries
  - exponential backoff
- transient SQL detection (timeouts, deadlocks, service busy/unavailable)

### Observability
- `ILogger` hooks for lifecycle/failure/retry diagnostics
- OpenTelemetry support via `ActivitySource`:
  - source name: `SharpFunctional.MsSql`
  - transaction activities
  - Dapper query/stored proc activities
  - retry events + success/failure tags

---

## Architecture

- `FunctionalMsSqlDb` - root facade and transaction orchestration
- `EfFunctionalDb` - functional EF Core operations
- `DapperFunctionalDb` - functional Dapper operations
- `TransactionExtensions` - transaction mapping helpers
- `FunctionalExtensions` - async functional composition (`Bind`, `Map`)
- `SqlExecutionOptions` - timeout/retry policy configuration
- `SharpFunctionalMsSqlDiagnostics` - tracing constants and `ActivitySource`
- `ServiceCollectionExtensions` - DI registration helpers
- `FunctionalMsSqlDbOptions` - options class for DI configuration

---

## Installation

```bash
dotnet add package SharpFunctional.MSSQL
```

---

## Dependency Injection

`SharpFunctional.MSSQL` integrates with `Microsoft.Extensions.DependencyInjection` via `IOptions<FunctionalMsSqlDbOptions>`.
`FunctionalMsSqlDb` is registered as **scoped** (one instance per request/scope).

### EF Core only

```csharp
// AppDbContext must already be registered
services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));

services.AddFunctionalMsSqlEf<AppDbContext>(opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
});
```

### Dapper only

```csharp
services.AddFunctionalMsSqlDapper(connectionString, opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(
        commandTimeoutSeconds: 30,
        maxRetryCount: 3,
        baseRetryDelay: TimeSpan.FromMilliseconds(200));
});
```

### Both backends (EF + Dapper)

```csharp
services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));

// Pass connection string directly
services.AddFunctionalMsSql<AppDbContext>(connectionString, opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60, maxRetryCount: 3);
});

// Or put everything in the options delegate
services.AddFunctionalMsSql<AppDbContext>(opts =>
{
    opts.ConnectionString = connectionString;
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
});
```

### Inject and use

```csharp
public class UserService(FunctionalMsSqlDb db)
{
    public async Task<Option<User>> GetUserAsync(int id, CancellationToken ct)
        => await db.Ef().GetByIdAsync<User, int>(id, ct);

    public async Task<Seq<OrderDto>> GetOrdersAsync(int userId, CancellationToken ct)
        => await db.Dapper().QueryAsync<OrderDto>(
            "SELECT * FROM Orders WHERE UserId = @UserId",
            new { UserId = userId },
            ct);
}
```

---

## Quick start

### 1) Configure facade

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql;
using SharpFunctional.MsSql.Common;

var options = new DbContextOptionsBuilder<MyDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var dbContext = new MyDbContext(options);
await using var connection = new SqlConnection(connectionString);

var executionOptions = new SqlExecutionOptions(
    commandTimeoutSeconds: 30,
    maxRetryCount: 2,
    baseRetryDelay: TimeSpan.FromMilliseconds(100),
    maxRetryDelay: TimeSpan.FromSeconds(2));

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<FunctionalMsSqlDb>();

var db = new FunctionalMsSqlDb(
    dbContext: dbContext,
    connection: connection,
    executionOptions: executionOptions,
    logger: logger);
```

### 2) EF functional query

```csharp
var user = await db.Ef().GetByIdAsync<User, int>(42, cancellationToken);

user.IfSome(u => Console.WriteLine($"Found: {u.Name}"));
user.IfNone(() => Console.WriteLine("User not found"));
```

### 3) Dapper functional query

```csharp
var rows = await db.Dapper().QueryAsync<UserDto>(
    "SELECT Id, Name FROM Users WHERE IsActive = @IsActive",
    new { IsActive = true },
    cancellationToken);
```

### 4) Transaction flow

```csharp
var result = await db.InTransactionAsync(async txDb =>
{
    var add = await txDb.Ef().AddAsync(new User { Name = "Ada" }, cancellationToken);
    if (add.IsFail) return LanguageExt.Prelude.FinFail<string>(LanguageExt.Common.Error.New("Add failed"));

    await dbContext.SaveChangesAsync(cancellationToken);
    return LanguageExt.Fin<string>.Succ("committed");
}, cancellationToken);
```

---

## OpenTelemetry integration

Use the source name below in your tracer configuration:

```text
SharpFunctional.MsSql
```

Emitted telemetry includes:
- transaction activities (EF and Dapper)
- Dapper operation activities
- retry events and standardized tags (`backend`, `operation`, `success`, `retry.attempt`)

---

## Build

```bash
dotnet restore
dotnet build SharpFunctional.MSSQL.slnx -c Release
```

## Pack NuGet

```bash
dotnet pack src/SharpFunctional.MSSQL/SharpFunctional.MSSQL.csproj -c Release -o ./artifacts/packages
```

Generates:
- `.nupkg`
- `.snupkg`

---

## Tests

```bash
dotnet test tests/SharpFunctional.MSSQL.Tests
```

Test suite uses `xUnit v3` and includes LocalDB-backed integration tests.

---

## Repository structure

- `src/` - library source
- `tests/` - test projects
- `examples/` - runnable sample app
- `docs/` - extra docs
- `.github/` - CI/CD and repo automation

---

## Release process

1. Create and push a semantic version tag (for example `v0.1.0`).
2. GitHub Actions builds and packs the library.
3. Release workflow publishes to NuGet.org using `NUGET_API_KEY`.

---

## License

MIT. See `LICENSE`.

---

## Contributing

Issues and pull requests are welcome.
