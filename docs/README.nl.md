# SharpFunctional.MSSQL

<p align="center">
  <img src="../sharpfunctional_mssql_logo_512.png" alt="SharpFunctional.MSSQL" width="120" />
</p>

[![NuGet](https://img.shields.io/nuget/v/SharpFunctional.MSSQL.svg)](https://www.nuget.org/packages/SharpFunctional.MSSQL)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SharpFunctional.MSSQL.svg)](https://www.nuget.org/packages/SharpFunctional.MSSQL)
[![CI](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/ci.yml)
[![NuGet Publish](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](../LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/)
[![C%23](https://img.shields.io/badge/C%23-14-239120)](https://learn.microsoft.com/dotnet/csharp/)

[Nederlands](README.nl.md) | [English](../README.md)

Functional-first SQL Server toegang voor moderne .NET-applicaties.

`SharpFunctional.MSSQL` is een `.NET 10` / `C# 14` library die combineert:
- **Entity Framework Core** gebruiksgemak
- **Dapper** performance
- **LanguageExt** resultaattypes (`Option<T>`, `Seq<T>`, `Fin<T>`)
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

---

## Features

### Functioneel API-model
- `Option<T>` voor optionele waarden
- `Seq<T>` voor queryresultaten
- `Fin<T>` voor success/failure met foutcontext

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

### Dapper integratie (`DapperFunctionalDb`)
- `QueryAsync<T>`
- `QuerySingleAsync<T>`
- `ExecuteStoredProcAsync<T>`
- `ExecuteStoredProcSingleAsync<T>`
- `ExecuteStoredProcNonQueryAsync`

### Transaction support (`FunctionalMsSqlDb`)
- `InTransactionAsync<T>` voor commit/rollback flows
- `InTransactionMapAsync<TIn, TOut>` voor transactionele mapping
- ondersteuning voor zowel EF- als Dapper-backend

### Async + cancellation first
- `CancellationToken` op alle publieke async API’s
- cancel-propagatie door EF, Dapper, retries en helper-extensies

### Resilience opties
- `SqlExecutionOptions` voor:
  - command timeout
  - max retries
  - exponential backoff
- transient SQL detectie (timeouts, deadlocks, service busy/unavailable)

### Observability
- `ILogger` hooks voor lifecycle/failure/retry diagnostiek
- OpenTelemetry support via `ActivitySource`:
  - source name: `SharpFunctional.MsSql`
  - transaction activities
  - Dapper query/stored procedure activities
  - retry events + success/failure tags

---

## Architectuur

- `FunctionalMsSqlDb` - root facade en transaction orchestration
- `EfFunctionalDb` - functionele EF Core operaties
- `DapperFunctionalDb` - functionele Dapper operaties
- `TransactionExtensions` - transaction mapping helpers
- `FunctionalExtensions` - async functionele compositie (`Bind`, `Map`)
- `SqlExecutionOptions` - timeout/retry policy configuratie
- `SharpFunctionalMsSqlDiagnostics` - tracing constants en `ActivitySource`

---

## Installatie

```bash
dotnet add package SharpFunctional.MSSQL
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

---

## OpenTelemetry integratie

Gebruik de volgende source name in je tracer-configuratie:

```text
SharpFunctional.MsSql
```

Geëmitteerde telemetry bevat:
- transaction activities (EF en Dapper)
- Dapper operation activities
- retry events + gestandaardiseerde tags (`backend`, `operation`, `success`, `retry.attempt`)

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

- `src/` - library source
- `tests/` - testprojecten
- `examples/` - runnable voorbeeldapp
- `docs/` - extra documentatie
- `.github/` - CI/CD en repo-automatisering

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