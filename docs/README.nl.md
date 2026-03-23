# SharpFunctional.MSSQL

<p align="center">
  <img src="../sharpfunctional_mssql_logo_512.png" alt="SharpFunctional.MSSQL" width="120" />
</p>

[![NuGet](https://img.shields.io/nuget/v/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpFunctional.MsSql.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![CI](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml)
[![NuGet Publish](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download)
[![NuGet](https://img.shields.io/badge/NuGet-3.0.0-blue.svg)](https://www.nuget.org/packages/SharpFunctional.MsSql)
[![Tests](https://img.shields.io/badge/Tests-150%2B-brightgreen.svg)](#testing)
[![C#](https://img.shields.io/badge/C%23-14-purple.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)

[Nederlands](README.nl.md) | [English](../README.md)

Functional-first SQL Server toegang voor moderne .NET-applicaties.

`SharpFunctional.MSSQL` is een `.NET 10` / `C# 14` library die combineert:
- **Entity Framework Core** gebruiksgemak
- **Dapper** performance
- **Ingebouwde functionele types** (`Option<T>`, `Seq<T>`, `Fin<T>`) — zero externe dependencies
- **No-exception API-oppervlak** voor verwachte foutpaden

---

## Waarom SharpFunctional.MSSQL?

Deze package helpt je SQL Server data-access te bouwen met:
- expliciete success/failure flows
- composable async operaties
- transaction-safe uitvoering
- structured logging
- ingebouwde retry/timeout configuratie
- OpenTelemetry tracing hooks
- server-side paginatie met navigatie-metadata
- specification pattern voor herbruikbare queries
- batch insert/update/delete operaties
- `IAsyncEnumerable<T>` streaming voor grote datasets
- circuit breaker resilience pattern

---

## Features

### Functioneel API-model (zero-dependency, ingebouwd)
- `Option<T>` voor optionele waarden
- `Seq<T>` voor queryresultaten (gebaseerd op `ImmutableArray<T>`)
- `Fin<T>` voor success/failure met foutcontext
- `Unit` als void-vervanging
- `Error` voor gestructureerde foutrepresentatie

### EF Core integratie (`EfFunctionalDb`)
- `GetByIdAsync<T, TId>`
- `FindOneAsync<T>`
- `QueryAsync<T>`
- `AddAsync<T>`
- `SaveAsync<T>`
- `DeleteByIdAsync<T, TId>`
- `CountAsync<T>`
- `AnyAsync<T>`
- expliciete `WithTracking()` modus
- `FindPaginatedAsync<T>` — server-side paginatie met `QueryResults<T>`
- `FindAsync<T>(IQuerySpecification<T>)` — specification pattern queries
- `InsertBatchAsync<T>` — configureerbare batch inserts
- `UpdateBatchAsync<T>` — batch updates met ondersteuning voor detached entities
- `DeleteBatchAsync<T>` — predicate-gebaseerde batch deletes
- `StreamAsync<T>` — `IAsyncEnumerable<T>` streaming voor grote datasets

### Dapper integratie (`DapperFunctionalDb`)
- `QueryAsync<T>`
- `QuerySingleAsync<T>`
- `ExecuteStoredProcAsync<T>`
- `ExecuteStoredProcSingleAsync<T>`
- `ExecuteStoredProcNonQueryAsync`
- `ExecuteStoredProcPaginatedAsync<T>` — gepagineerde stored procedure resultaten via `QueryMultipleAsync`

### Transaction support (`FunctionalMsSqlDb`)
- `InTransactionAsync<T>` voor commit/rollback flows
- `InTransactionMapAsync<TIn, TOut>` voor transactionele mapping
- ondersteuning voor zowel EF- als Dapper-backend

### Async + cancellation first
- `CancellationToken` op alle publieke async API's
- cancel-propagatie door EF, Dapper, retries en helper-extensies

### Resilience opties
- `SqlExecutionOptions` voor:
  - command timeout
  - max retries
  - exponential backoff
- transient SQL detectie (timeouts, deadlocks, service busy/unavailable)
- `CircuitBreaker` pattern:
  - thread-safe state machine (`Closed` → `Open` → `HalfOpen`)
  - configureerbare failure threshold, open duration en half-open success threshold
  - functionele `ExecuteAsync<T>` die `Fin<T>` retourneert

### Observability
- `ILogger` hooks voor lifecycle/failure/retry diagnostiek
- OpenTelemetry support via `ActivitySource`:
  - source name: `SharpFunctional.MsSql`
  - transaction activities
  - Dapper operation activities (`dapper.query.seq`, `dapper.query.single`, `dapper.storedproc.*`)
  - EF Core operation activities (`ef.find.paginated`, `ef.find.spec`, `ef.batch.insert`, `ef.batch.update`, `ef.batch.delete`)
  - retry events en gestandaardiseerde tags (`backend`, `operation`, `success`, `retry.attempt`)
  - uitgebreide tags: `entity_type`, `batch_size`, `item_count`, `page_number`, `page_size`, `duration_ms`, `correlation_id`, `circuit_state`

---

## Architectuur

- `FunctionalMsSqlDb` - root facade en transaction orchestration
- `EfFunctionalDb` - functionele EF Core operaties (CRUD, paginatie, batch, streaming, specification)
- `DapperFunctionalDb` - functionele Dapper operaties (queries, stored procedures, paginatie)
- `TransactionExtensions` - transaction mapping helpers
- `FunctionalExtensions` - async functionele compositie (`Bind`, `Map`)
- `SqlExecutionOptions` - timeout/retry policy configuratie
- `SharpFunctionalMsSqlDiagnostics` - tracing constants en `ActivitySource`
- `CircuitBreaker` - thread-safe circuit breaker pattern voor database-operaties
- `CircuitBreakerOptions` - circuit breaker configuratie (thresholds, durations)
- `QueryResults<T>` - gepagineerd queryresultaat met navigatie-metadata
- `IQuerySpecification<T>` / `QuerySpecification<T>` - herbruikbare, composable query specifications
- `Option<T>` / `Fin<T>` / `Seq<T>` / `Unit` / `Error` - zero-dependency functionele types (vervangt LanguageExt)
- `Prelude` - statische helpers (`FinFail`, `FinSucc`, `toSeq`, `Optional`, `unit`)
- `ServiceCollectionExtensions` - DI registratie helpers
- `FunctionalMsSqlDbOptions` - opties voor DI configuratie

---

## Installatie

```bash
dotnet add package SharpFunctional.MSSQL
```

---

## Dependency Injection

`SharpFunctional.MSSQL` integreert met `Microsoft.Extensions.DependencyInjection` via `IOptions<FunctionalMsSqlDbOptions>`.
`FunctionalMsSqlDb` wordt geregistreerd als **scoped** (één instantie per request/scope).

### Alleen EF Core

```csharp
// AppDbContext moet al geregistreerd zijn
services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));

services.AddFunctionalMsSqlEf<AppDbContext>(opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
});
```

### Alleen Dapper

```csharp
services.AddFunctionalMsSqlDapper(connectionString, opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(
        commandTimeoutSeconds: 30,
        maxRetryCount: 3,
        baseRetryDelay: TimeSpan.FromMilliseconds(200));
});
```

### Beide backends (EF + Dapper)

```csharp
services.AddDbContext<AppDbContext>(opts => opts.UseSqlServer(connectionString));

// Connection string direct meegeven
services.AddFunctionalMsSql<AppDbContext>(connectionString, opts =>
{
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60, maxRetryCount: 3);
});

// Of alles via de opties delegate
services.AddFunctionalMsSql<AppDbContext>(opts =>
{
    opts.ConnectionString = connectionString;
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 60);
});
```

### Injecteren en gebruiken

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

### 1) Configureer de facade

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

### 2) EF functionele query

```csharp
var user = await db.Ef().GetByIdAsync<User, int>(42, cancellationToken);

user.IfSome(u => Console.WriteLine($"Found: {u.Name}"));
user.IfNone(() => Console.WriteLine("User not found"));
```

### 3) Dapper functionele query

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

### 5) Gepagineerde query

```csharp
var page = await db.Ef().FindPaginatedAsync<User>(
    u => u.IsActive,
    pageNumber: 2,
    pageSize: 25,
    cancellationToken);

page.Match(
    Succ: results =>
    {
        Console.WriteLine($"Pagina {results.PageNumber}/{results.TotalPages} ({results.TotalCount} totaal)");
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

orders.IfSome(list => Console.WriteLine($"Gevonden: {list.Count} orders"));
```

### 7) Batch operaties

```csharp
// Batch insert
var newUsers = Enumerable.Range(1, 500).Select(i => new User { Name = $"User{i}" });
var inserted = await db.Ef().InsertBatchAsync(newUsers, batchSize: 100, cancellationToken);

// Batch update
var updated = await db.Ef().WithTracking().UpdateBatchAsync(modifiedUsers, batchSize: 100, cancellationToken);

// Batch delete
var deleted = await db.Ef().DeleteBatchAsync<User>(u => u.IsActive == false, batchSize: 200, cancellationToken);
```

### 8) Grote resultaten streamen

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

var breaker = new CircuitBreaker(options);

var result = await breaker.ExecuteAsync(
    async ct => await db.Ef().GetByIdAsync<User, int>(42, ct),
    cancellationToken);

// result is Fin<Option<User>> — controleer breaker status
Console.WriteLine($"Circuit status: {breaker.State}");
```

### 10) Dapper gepagineerde stored procedure

```csharp
var page = await db.Dapper().ExecuteStoredProcPaginatedAsync<OrderDto>(
    "usp_GetOrders",
    new { StatusId = 1, PageNumber = 1, PageSize = 50 },
    cancellationToken);

page.Match(
    Succ: results => Console.WriteLine($"Pagina {results.PageNumber} van {results.TotalPages}"),
    Fail: error => Console.WriteLine(error));
```

---

## OpenTelemetry integratie

Gebruik de volgende source name in je tracer-configuratie:

```text
SharpFunctional.MsSql
```

Geëmitteerde telemetry bevat:
- transaction activities (EF en Dapper)
- Dapper operation activities (`dapper.query.seq`, `dapper.query.single`, `dapper.storedproc.*`)
- EF Core operation activities (`ef.find.paginated`, `ef.find.spec`, `ef.batch.insert`, `ef.batch.update`, `ef.batch.delete`)
- retry events en gestandaardiseerde tags (`backend`, `operation`, `success`, `retry.attempt`)
- uitgebreide diagnostische tags:
  - `entity_type` — het entity CLR-typenaam
  - `batch_size` — batchgrootte voor bulkoperaties
  - `item_count` — aantal beïnvloede items
  - `page_number` / `page_size` — paginatieparameters
  - `duration_ms` — operatieduur in milliseconden
  - `correlation_id` — koppelt gerelateerde operaties
  - `circuit_state` — circuit breaker status

---

## Build

```bash
dotnet restore
dotnet build SharpFunctional.MSSQL.slnx -c Release
```

## NuGet pack

```bash
dotnet pack src/SharpFunctional.MSSQL/SharpFunctional.MSSQL.csproj -c Release -o ./artifacts/packages
```

Genereert:
- `.nupkg`
- `.snupkg`

---

## Tests

```bash
dotnet test tests/SharpFunctional.MSSQL.Tests
```

De testsuite gebruikt `xUnit v3` en bevat LocalDB-gebaseerde integratietests.

---

## Repository-structuur

- `src/` — library broncode
- `tests/` — xUnit v3 testsuite (150+ tests)
- `examples/` — uitvoerbare voorbeeldapplicaties:
  - `SharpFunctional.MSSQL.Example` — uitgebreide console-app (16 secties met CRUD, aggregaten, functionele chaining, Dapper, transacties, paginatie, specification pattern, batch operaties, streaming, circuit breaker en DI)
  - `SharpFunctional.MSSQL.DI.Example` — dependency injection voorbeeld met `ProductService` dat alle drie registratie-overloads, paginatie, specification queries, batch inserts, streaming en circuit breaker demonstreert
- `docs/` — aanvullende documentatie
- `.github/` — CI/CD en repo-automatisering
- `CHANGELOG.md` — versiegeschiedenis
- `MIGRATION_v1_to_v2.md` — upgrade-handleiding van v1 naar v2

---

## Voorbeelden

Twee uitvoerbare console-applicaties demonstreren alle features van de library:

### Volledig voorbeeld (`examples/SharpFunctional.MSSQL.Example`)

Een uitgebreide walkthrough met 16 secties:

| Sectie | Feature |
|--------|---------|
| 1–2 | Database setup en seed data (klanten, producten, orders) |
| 3 | EF Core queries — `GetByIdAsync`, `FindOneAsync`, `QueryAsync` |
| 4 | Aggregaten — `CountAsync`, `AnyAsync` |
| 5 | Functionele chaining — `Option → Seq`, `Option → Option`, `Seq → Seq` |
| 6 | Dapper queries — raw SQL, enkel resultaat, joins |
| 7–8 | Transacties en `InTransactionMapAsync` |
| 9 | Verwijderen en verifiëren |
| 10 | **Gepagineerde queries** — `FindPaginatedAsync` met `QueryResults<T>.Map` |
| 11 | **Specification pattern** — `QuerySpecification<T>` met sortering en skip/take |
| 12 | **Batch operaties** — `InsertBatchAsync`, `UpdateBatchAsync`, `DeleteBatchAsync` |
| 13 | **Streaming** — `StreamAsync<T>` met `await foreach` |
| 14 | **Circuit breaker** — succes, trip naar Open, afwijzing, reset |
| 15–16 | Eindoverzicht en DI container demo |

### DI voorbeeld (`examples/SharpFunctional.MSSQL.DI.Example`)

Demonstreert `FunctionalMsSqlDb` registratie en gebruik via constructor injection:

- Alle drie registratie-overloads: alleen EF, alleen Dapper, EF + Dapper gecombineerd
- `ProductService` met methoden voor: `GetByIdAsync`, `GetByCategoryAsync`, `CountInStockAsync`, `GetSummariesAsync`, `GetPaginatedAsync`, `GetBySpecificationAsync`, `BatchInsertAsync`, `StreamAllAsync`, `AddProductAsync`
- Circuit breaker integratie die service-aanroepen omhult

```bash
# Voer het volledige voorbeeld uit (vereist SQL Server LocalDB)
cd examples/SharpFunctional.MSSQL.Example
dotnet run

# Voer het DI voorbeeld uit
cd examples/SharpFunctional.MSSQL.DI.Example
dotnet run
```

---

## Releaseproces

1. Maak en push een semantic version tag (bijvoorbeeld `v0.1.0`).
2. GitHub Actions bouwt en pakt de library.
3. De release-workflow publiceert naar NuGet.org met `NUGET_API_KEY`.

---

## Licentie

MIT. Zie `LICENSE`.

---

## Contributing

Issues en pull requests zijn welkom.
