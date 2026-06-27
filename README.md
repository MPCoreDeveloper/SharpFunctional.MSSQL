# SharpFunctional.MSSQL

<p align="center">
  <img src="sharpfunctional_mssql_logo_512.png" alt="SharpFunctional.MSSQL" width="120" />
</p>

[![NuGet](https://img.shields.io/nuget/v/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)
[![NuGet](https://img.shields.io/badge/NuGet-3.2.1-blue.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![Tests](https://img.shields.io/badge/Tests-160%2B-brightgreen.svg)](#testing)
[![C#](https://img.shields.io/badge/C%23-14-purple.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink.svg)](https://github.com/sponsors/MPCoreDeveloper)

[English](README.md) | [Nederlands](docs/README.nl.md)

Functional-first SQL Server access for modern .NET.

`SharpFunctional.MSSQL` is a `.NET 10` / `C# 14` library that combines:
- **Entity Framework Core** convenience
- **Dapper** performance
- **Built-in functional types** (`Option<T>`, `Seq<T>`, `Fin<T>`) — zero external dependencies
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
- server-side pagination with navigation metadata
- specification pattern for reusable queries
- batch insert/update/delete operations
- `IAsyncEnumerable<T>` streaming for large data sets
- circuit breaker resilience pattern
- configurable retry jitter strategy
- custom telemetry activity enrichment hooks

---

## What's new (v1.0.0 → v3.2.1)

### v3.2.1 — Updated to latest stable dependencies

This is a maintenance release that updates all primary dependencies to their latest stable versions compatible with .NET 10.

**Key packages updated:**

| Package | Version |
|---------|---------|
| `Microsoft.EntityFrameworkCore` | 10.0.9 |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.9 |
| `Microsoft.Data.SqlClient` | 7.0.2 |
| `Dapper` | 2.1.79 |
| `Microsoft.Extensions.Logging` + `Abstractions` | 10.0.9 |
| `Microsoft.Extensions.Options` | 10.0.9 |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.9 |

No public API or behavioral changes. The library continues to target **.NET 10** and **C# 14**.

### v3.2.0 — Expanded functional API, Dapper improvements, and full telemetry coverage

This release is fully **backwards compatible** — all changes are additive.

**New methods on built-in functional types:**

| Type | New method | Description |
|---|---|---|
| `Fin<T>` | `MapAsync<TResult>(Func<T, Task<TResult>>)` | Async transform of the success value |
| `Fin<T>` | `BindAsync<TResult>(Func<T, Task<Fin<TResult>>>)` | Async flat-map of the success value |
| `Fin<T>` | `ToOption()` | Convert to `Option<T>` (`Succ` → `Some`, `Fail` → `None`) |
| `Option<T>` | `ToFin(Error ifNone)` | Convert to `Fin<T>` (`Some` → `Succ`, `None` → `Fail(ifNone)`) |
| `Seq<T>` | `Bind<TResult>(Func<T, Seq<TResult>>)` | Flat-map / SelectMany over the sequence |
| `FunctionalExtensions` | `Map<TIn,TOut>(this Task<Fin<TIn>>, Func<TIn,TOut>)` | Async map extension for `Task<Fin<T>>` |

**Dapper improvements:**
- Parameterless overloads for all five Dapper query/proc methods — no longer required to pass `new { }` or `null` when a stored procedure or query takes no parameters:
  - `ExecuteStoredProcSingleAsync<T>(procName, ct)`
  - `ExecuteStoredProcAsync<T>(procName, ct)`
  - `ExecuteStoredProcNonQueryAsync(procName, ct)`
  - `QueryAsync<T>(sql, ct)`
  - `QuerySingleAsync<T>(sql, ct)`
- Source-generated `[LoggerMessage]` logging throughout `DapperFunctionalDb` — matches the zero-allocation pattern already used in `FunctionalMsSqlDb`. Direct `Logger?.LogDebug/Error/Warning` calls replaced with source-generated `DapperFunctionalDbLog` methods.

**OpenTelemetry completeness:**
- `EfFunctionalDb.GetByIdAsync`, `FindOneAsync`, and `QueryAsync` now emit activities — previously the only three EF Core methods without tracing. All EF operations are now fully covered.

**Bug fix:**
- `Fin<T>.IfFail(Func<Error, T>)` XML documentation was incorrect (stated "throws" instead of "invokes handler").

---

### What's added/changed between v3.0.0 and v3.1.1

The `3.0.1` and `3.0.2` releases are fully **backwards compatible** and focus on resilience and observability improvements.

**Added:**
- `RetryJitterMode` in `SqlExecutionOptions` (`None` default, `Full` opt-in)
- optional `ActivityEnricher` delegate in `SqlExecutionOptions` for custom OpenTelemetry `Activity` enrichment
- `CircuitBreakerSnapshot` immutable diagnostics model
- `CircuitBreaker.GetSnapshot()` for thread-safe state/counter/timing inspection

**Changed (non-breaking):**
- emitted activities in `FunctionalMsSqlDb`, `DapperFunctionalDb`, and `EfFunctionalDb` now support optional enrichment
- logging in `FunctionalMsSqlDb` now uses source-generated `LoggerMessage` methods to reduce allocations and improve diagnostics throughput
- this logging optimization was community-driven after a LinkedIn snippet code review request by Ewart Nijburg (Principal .NET & Azure Architect)
- enricher failures are handled defensively and do not break data access operations
- test coverage expanded for new resilience/telemetry behaviors

### v3.0.0 — Zero-dependency functional types

The `LanguageExt.Core` dependency has been **completely removed**.
All functional types (`Option<T>`, `Fin<T>`, `Seq<T>`, `Unit`, `Error`) are now built-in lightweight `readonly struct` implementations in the `SharpFunctional.MsSql.Functional` namespace — purpose-built for this library.

| What changed | Before (v1/v2) | After (v3) |
|---|---|---|
| **Functional types** | `LanguageExt.Core` (4.4.9, >200 types) | Built-in: 5 types + `Prelude` |
| **External dependencies** | LanguageExt + transitive deps | Zero functional deps |
| **Import** | `using LanguageExt;` | `using SharpFunctional.MsSql.Functional;` |
| **API surface** | Identical | Identical — drop-in replacement |

**Migration from v2:**
```csharp
// Replace:
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

// With:
using SharpFunctional.MsSql.Functional;
using static SharpFunctional.MsSql.Functional.Prelude;
```

All type names (`Option<T>`, `Fin<T>`, `Seq<T>`, `Error.New()`, `FinSucc()`, `FinFail()`, `toSeq()`) remain the same.

### v2.0.0 — Feature expansion + C# 14 modernization

Major feature additions built on top of the v1 foundation:

**New EF Core operations:**
- `FindPaginatedAsync<T>` — server-side pagination with `QueryResults<T>` (total pages, navigation metadata, `Map` projection)
- `FindAsync<T>(IQuerySpecification<T>)` — specification pattern with filter, include, ordering, and paging
- `InsertBatchAsync<T>` / `UpdateBatchAsync<T>` / `DeleteBatchAsync<T>` — configurable batch operations
- `StreamAsync<T>` — `IAsyncEnumerable<T>` streaming for large data sets

**New Dapper operation:**
- `ExecuteStoredProcPaginatedAsync<T>` — paginated stored procedure results via `QueryMultipleAsync`

**New common types:**
- `QueryResults<T>` — immutable pagination record
- `IQuerySpecification<T>` / `QuerySpecification<T>` — composable query specifications
- `CircuitBreaker` — thread-safe circuit breaker pattern (`Closed` → `Open` → `HalfOpen`)

**New diagnostics:**
- 8 new OpenTelemetry tags (`entity_type`, `batch_size`, `item_count`, `page_number`, `page_size`, `duration_ms`, `correlation_id`, `circuit_state`)
- EF Core activity tracing for all new methods

**C# 14 modernization:**
- Primary constructors on all core classes
- C# 14 `Lock` class (replaces `object` locks)
- Collection expressions throughout
- Zero breaking changes — all additive

### v1.0.0 — Initial release

Foundation of the functional SQL Server access library:
- `FunctionalMsSqlDb` facade with EF Core + Dapper backends
- `EfFunctionalDb` — 9 functional CRUD operations
- `DapperFunctionalDb` — 5 functional query/stored proc operations
- Transaction support (`InTransactionAsync`, `InTransactionMapAsync`)
- `SqlExecutionOptions` with retry/timeout configuration
- Transient SQL error detection
- OpenTelemetry `ActivitySource` integration
- DI registration via `ServiceCollectionExtensions`
- Full xUnit v3 test suite

---

## Features

### Functional API model (zero-dependency, built-in)
- `Option<T>` for optional values — `Map`, `Bind`, `Filter`, `IfSome`, `IfNone`, `ToFin(Error)`
- `Seq<T>` for query result sequences (backed by `ImmutableArray<T>`) — `Map`, `Filter`, `Bind` (flatMap), `AsSpan`
- `Fin<T>` for success/failure with error context — `Map`, `MapAsync`, `Bind`, `BindAsync`, `Match`, `ToOption`, `IfSucc`, `IfFail`
- `Unit` as void replacement
- `Error` for structured error representation
- `FunctionalExtensions` async composition: `Bind`, `Map` for `Task<Option<T>>`, `Task<Seq<T>>`, and `Task<Fin<T>>`

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
- `FindPaginatedAsync<T>` — server-side pagination with `QueryResults<T>`
- `FindAsync<T>(IQuerySpecification<T>)` — specification pattern queries
- `InsertBatchAsync<T>` — configurable batch inserts
- `UpdateBatchAsync<T>` — batch updates with detached-entity support
- `DeleteBatchAsync<T>` — predicate-based batch deletes
- `StreamAsync<T>` — `IAsyncEnumerable<T>` streaming for large data sets

### Dapper integration (`DapperFunctionalDb`)
- `QueryAsync<T>` (with or without parameters)
- `QuerySingleAsync<T>` (with or without parameters)
- `ExecuteStoredProcAsync<T>` (with or without parameters)
- `ExecuteStoredProcSingleAsync<T>` (with or without parameters)
- `ExecuteStoredProcNonQueryAsync` (with or without parameters)
- `ExecuteStoredProcPaginatedAsync<T>` — paginated stored procedure results via `QueryMultipleAsync`

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
  - optional retry jitter (`RetryJitterMode.Full`)
- transient SQL detection (timeouts, deadlocks, service busy/unavailable)
- `CircuitBreaker` pattern:
  - thread-safe state machine (`Closed` → `Open` → `HalfOpen`)
  - configurable failure threshold, open duration, and half-open success threshold
  - functional `ExecuteAsync<T>` returning `Fin<T>`
  - read-only `GetSnapshot()` metrics for state and counters

### Observability
- `ILogger` hooks for lifecycle/failure/retry diagnostics
- source-generated `LoggerMessage` logging in `FunctionalMsSqlDb` **and `DapperFunctionalDb`** — zero-allocation diagnostics at disabled log levels
- OpenTelemetry support via `ActivitySource`:
  - source name: `SharpFunctional.MsSql`
  - transaction activities
  - Dapper query/stored proc activities
  - EF Core activities — **full coverage** including `GetByIdAsync`, `FindOneAsync`, `QueryAsync`, pagination, batch insert/update/delete, specification queries
  - retry events and standardized tags (`backend`, `operation`, `success`, `retry.attempt`)
  - optional custom activity enrichment via `SqlExecutionOptions.ActivityEnricher`
  - extended tags: `entity_type`, `batch_size`, `item_count`, `page_number`, `page_size`, `duration_ms`, `correlation_id`, `circuit_state`

---

## Architecture

- `FunctionalMsSqlDb` - root facade and transaction orchestration
- `EfFunctionalDb` - functional EF Core operations (CRUD, pagination, batch, streaming, specification)
- `DapperFunctionalDb` - functional Dapper operations (queries, stored procedures, pagination)
- `TransactionExtensions` - transaction mapping helpers
- `FunctionalExtensions` - async functional composition (`Bind`, `Map`)
- `SqlExecutionOptions` - timeout/retry policy configuration
- `SharpFunctionalMsSqlDiagnostics` - tracing constants and `ActivitySource`
- `CircuitBreaker` - thread-safe circuit breaker pattern for database operations
- `CircuitBreakerOptions` - circuit breaker configuration (thresholds, durations)
- `QueryResults<T>` - paginated query result with navigation metadata
- `IQuerySpecification<T>` / `QuerySpecification<T>` - reusable, composable query specifications
- `Option<T>` / `Fin<T>` / `Seq<T>` / `Unit` / `Error` - zero-dependency functional types (replaces LanguageExt)
- `Prelude` - static helpers (`FinFail`, `FinSucc`, `toSeq`, `Optional`, `unit`)
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
using static SharpFunctional.MsSql.Functional.Prelude;

var result = await db.InTransactionAsync(async txDb =>
{
    var add = await txDb.Ef().AddAsync(new User { Name = "Ada" }, cancellationToken);
    if (add.IsFail) return FinFail<string>(Error.New("Add failed"));

    await dbContext.SaveChangesAsync(cancellationToken);
    return FinSucc("committed");
}, cancellationToken);
```

### 5) Paginated query

```csharp
var page = await db.Ef().FindPaginatedAsync<User>(
    u => u.IsActive,
    pageNumber: 2,
    pageSize: 25,
    cancellationToken);

page.Match(
    Succ: results =>
    {
        Console.WriteLine($"Page {results.PageNumber}/{results.TotalPages} ({results.TotalCount} total)");
        foreach (var user in results.Items)
            Console.WriteLine(user.Name);
    },
    Fail: error => Console.WriteLine(error));
```

### 6) Specification pattern

```csharp
var spec = new QuerySpecification<Order>(o => o.Total > 1000)
    .SetOrderByDescending(o => o.OrderDate)
    .SetSkip(50)
    .SetTake(25);

var orders = await db.Ef().FindAsync(spec, cancellationToken);

orders.IfSome(list => Console.WriteLine($"Found {list.Count} orders"));
```

### 7) Batch operations

```csharp
// Insert batch
var newUsers = Enumerable.Range(1, 500).Select(i => new User { Name = $"User{i}" });
var inserted = await db.Ef().InsertBatchAsync(newUsers, batchSize: 100, cancellationToken);

// Update batch
var updated = await db.Ef().WithTracking().UpdateBatchAsync(modifiedUsers, batchSize: 100, cancellationToken);

// Delete batch
var deleted = await db.Ef().DeleteBatchAsync<User>(u => u.IsActive == false, batchSize: 200, cancellationToken);
```

### 8) Streaming large results

```csharp
await foreach (var user in db.Ef().StreamAsync<User>(u => u.IsActive, cancellationToken))
{
    await ProcessUserAsync(user, cancellationToken);
}
```

### 9) Circuit breaker

```csharp
var options = new CircuitBreakerOptions
{
    FailureThreshold = 5,
    OpenDuration = TimeSpan.FromSeconds(30),
    SuccessThresholdInHalfOpen = 2
};

var breaker = new CircuitBreaker(options, TimeProvider.System);

var result = await breaker.ExecuteAsync(
    async ct => await db.Ef().GetByIdAsync<User, int>(42, ct),
    cancellationToken);

// result is Fin<Option<User>> — check breaker state
Console.WriteLine($"Circuit state: {breaker.State}");

// Snapshot metrics (additive API)
var snapshot = breaker.GetSnapshot();
Console.WriteLine($"Failures: {snapshot.FailureCount}, In state for: {snapshot.TimeInState}");
```

### 10) Dapper paginated stored procedure

```csharp
var page = await db.Dapper().ExecuteStoredProcPaginatedAsync<OrderDto>(
    "usp_GetOrders",
    new { StatusId = 1, PageNumber = 1, PageSize = 50 },
    cancellationToken);

page.Match(
    Succ: results => Console.WriteLine($"Page {results.PageNumber} of {results.TotalPages}"),
    Fail: error => Console.WriteLine(error));
```

### 11) Parameterless Dapper calls

```csharp
// No parameters needed — just omit the param argument
var all = await db.Dapper().QueryAsync<UserDto>("SELECT * FROM Users", cancellationToken);
var next = await db.Dapper().ExecuteStoredProcAsync<OrderDto>("usp_GetPendingOrders", cancellationToken);
```

### 12) Async functional composition

```csharp
// MapAsync / BindAsync on Fin<T>
var dto = await db.Ef()
    .GetByIdAsync<User, int>(42, cancellationToken)
    .Bind(user => db.Ef().FindOneAsync<Profile>(p => p.UserId == user.Id, cancellationToken))
    .Map(profile => new ProfileDto(profile!.Bio));

// Convert between Option<T> and Fin<T>
Option<User> option = await db.Ef().FindOneAsync<User>(u => u.Email == email, cancellationToken);
Fin<User> fin = option.ToFin(Error.New("User not found"));

Fin<User> finResult = await db.Ef()
    .GetByIdAsync<User, int>(1, cancellationToken)
    .Bind(opt => Task.FromResult(opt.ToFin(Error.New("Not found"))));
Option<User> back = finResult.ToOption();

// Bind (flatMap) on Seq<T>
Seq<Tag> allTags = tagGroups.Bind(group => group.Tags);
```

---

## OpenTelemetry integration

Use the source name below in your tracer configuration:

```text
SharpFunctional.MsSql
```

You can optionally enrich each emitted activity via `SqlExecutionOptions.ActivityEnricher`.

Emitted telemetry includes:
- transaction activities (EF and Dapper)
- Dapper operation activities (`dapper.query.seq`, `dapper.query.single`, `dapper.storedproc.*`)
- EF Core operation activities (`ef.getbyid`, `ef.findone`, `ef.query`, `ef.find.paginated`, `ef.find.spec`, `ef.batch.insert`, `ef.batch.update`, `ef.batch.delete`)
- retry events and standardized tags (`backend`, `operation`, `success`, `retry.attempt`)
- extended diagnostic tags:
  - `entity_type` — the entity CLR type name
  - `batch_size` — batch size for bulk operations
  - `item_count` — number of affected items
  - `page_number` / `page_size` — pagination parameters
  - `duration_ms` — operation duration in milliseconds
  - `correlation_id` — links related operations
  - `circuit_state` — circuit breaker state

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

All integration tests support both **Windows (LocalDB)** and **Linux/macOS (Docker SQL Server)** environments.

### Windows Setup

Tests use `xUnit v3` and automatically connect to SQL Server LocalDB:

```bash
dotnet test tests/SharpFunctional.MSSQL.Tests
```

**Prerequisite:** SQL Server LocalDB must be running:
```powershell
sqllocaldb start MSSQLLocalDB
```

### Linux/macOS Setup

Docker SQL Server is required. Start the container:

```bash
cd tests
docker-compose up -d
```

Then run tests:

```bash
dotnet test tests/SharpFunctional.MSSQL.Tests
```

**Details:** See `tests/SharpFunctional.MSSQL.Tests/README.md` for comprehensive setup instructions, environment variables, troubleshooting, and CI/CD integration examples.

---

## Repository structure

- `src/` — library source
- `tests/` — xUnit v3 test suite (160+ tests)
- `examples/` — runnable sample applications:
  - `SharpFunctional.MSSQL.Example` — full-featured console app (16 sections covering CRUD, aggregates, functional chaining, Dapper, transactions, pagination, specification pattern, batch operations, streaming, circuit breaker, and DI)
  - `SharpFunctional.MSSQL.DI.Example` — dependency injection example with `ProductService` demonstrating all three registration overloads, pagination, specification queries, batch inserts, streaming, and circuit breaker
- `docs/` — additional documentation
- `.github/` — CI/CD and repo automation
- `CHANGELOG.md` — version history

---

## Examples

Two runnable console applications demonstrate every feature of the library:

### Full example (`examples/SharpFunctional.MSSQL.Example`)

A comprehensive 16-section walkthrough covering:

| Section | Feature |
|---------|---------|
| 1–2 | Database setup and seed data (customers, products, orders) |
| 3 | EF Core queries — `GetByIdAsync`, `FindOneAsync`, `QueryAsync` |
| 4 | Aggregates — `CountAsync`, `AnyAsync` |
| 5 | Functional chaining — `Option → Seq`, `Option → Option`, `Seq → Seq` |
| 6 | Dapper queries — raw SQL, single result, joins |
| 7–8 | Transactions and `InTransactionMapAsync` |
| 9 | Delete and verify |
| 10 | **Paginated queries** — `FindPaginatedAsync` with `QueryResults<T>.Map` |
| 11 | **Specification pattern** — `QuerySpecification<T>` with ordering and skip/take |
| 12 | **Batch operations** — `InsertBatchAsync`, `UpdateBatchAsync`, `DeleteBatchAsync` |
| 13 | **Streaming** — `StreamAsync<T>` with `await foreach` |
| 14 | **Circuit breaker** — success, trip to Open, rejection, reset |
| 15–16 | Final summary and DI container demo |

### DI example (`examples/SharpFunctional.MSSQL.DI.Example`)

Demonstrates `FunctionalMsSqlDb` registration and consumption via constructor injection:

- All three registration overloads: EF-only, Dapper-only, EF + Dapper combined
- `ProductService` with methods for: `GetByIdAsync`, `GetByCategoryAsync`, `CountInStockAsync`, `GetSummariesAsync`, `GetPaginatedAsync`, `GetBySpecificationAsync`, `BatchInsertAsync`, `StreamAllAsync`, `AddProductAsync`
- Circuit breaker integration wrapping service calls

```bash
# Run the full example (requires SQL Server LocalDB)
cd examples/SharpFunctional.MSSQL.Example
dotnet run

# Run the DI example
cd examples/SharpFunctional.MSSQL.DI.Example
dotnet run
```

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
