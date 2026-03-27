File: CHANGELOG.md
# Changelog

All notable changes to **SharpFunctional.MSSQL** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.1.0] - 2026-03-26

### Added
- `RetryJitterMode` enum for opt-in retry delay jitter behavior (`None`, `Full`)
- `SqlExecutionOptions.ActivityEnricher` delegate for custom activity tag enrichment
- `CircuitBreakerSnapshot` immutable diagnostics model
- `CircuitBreaker.GetSnapshot()` for thread-safe state/counter/timing inspection

### Changed
- `SqlExecutionOptions` constructor extended with additive optional parameters:
  - `retryJitterMode`
  - `activityEnricher`
- `FunctionalMsSqlDb`, `DapperFunctionalDb`, and `EfFunctionalDb` now apply optional activity enrichers to emitted `Activity` instances

### Compatibility
- Existing constructor calls and API usage remain valid
- Deterministic retry behavior remains default (`RetryJitterMode.None`)
- Activity enricher failures are ignored to avoid impacting operation flow

## [3.0.0] - 2025-07-23

### Removed
- **LanguageExt.Core** dependency — replaced with zero-dependency built-in functional types

### Added
- `SharpFunctional.MsSql.Functional` namespace with:
  - `Option<T>` — optional value struct (`Some`/`None`, `Match`, `IfSome`, `IfNone`, `Map`, `Bind`)
  - `Fin<T>` — result monad struct (`Succ`/`Fail`, `Match`, `Map`, `Bind`, `IfSucc`, `IfFail`)
  - `Seq<T>` — immutable sequence backed by `ImmutableArray<T>` (implements `IReadOnlyList<T>`)
  - `Unit` — void replacement struct
  - `Error` — structured error class with `New(string)`, `New(Exception)`, implicit conversions
  - `Prelude` — static helpers (`FinFail`, `FinSucc`, `toSeq`, `Seq<T>()`, `Optional`, `Some`, `None`, `unit`)

### Changed
- All `using LanguageExt` / `using static LanguageExt.Prelude` replaced with `using SharpFunctional.MsSql.Functional`
- NuGet package description updated (no longer references LanguageExt)
- Package tags updated (removed `languageext`)
- `FunctionalMsSqlDb` logging now uses source-generated `LoggerMessage` methods for lower-allocation package diagnostics

### Migration
- Replace `using LanguageExt;` / `using LanguageExt.Common;` with `using SharpFunctional.MsSql.Functional;`
- Replace `using static LanguageExt.Prelude;` with `using static SharpFunctional.MsSql.Functional.Prelude;`
- All type names and method signatures remain identical — `Option<T>`, `Fin<T>`, `Seq<T>`, `Error.New()`, `FinFail<T>()`, `toSeq()` all work the same way

## [2.0.0-preview.1] - 2025-07-17

### Added

#### EF Core operations
- `FindPaginatedAsync<T>` — server-side pagination returning `Fin<QueryResults<T>>` with clamped page/size
- `FindAsync<T>(IQuerySpecification<T>)` — specification pattern queries with filter, include, ordering, and paging
- `InsertBatchAsync<T>` — configurable batch inserts with `SaveChangesAsync` per batch
- `UpdateBatchAsync<T>` — batch updates with automatic detached-entity attachment
- `DeleteBatchAsync<T>` — predicate-based batch deletes in configurable chunks
- `StreamAsync<T>` — `IAsyncEnumerable<T>` streaming via `AsAsyncEnumerable()`

#### Dapper operations
- `ExecuteStoredProcPaginatedAsync<T>` — paginated stored procedure results via `QueryMultipleAsync`

#### Common types
- `QueryResults<T>` — immutable pagination record with `TotalPages`, `HasNextPage`, `HasPreviousPage`, `ItemsOnPage`, and `Map<TResult>` projection
- `IQuerySpecification<T>` — composable query specification interface
- `QuerySpecification<T>` — fluent builder with `AddInclude`, `SetOrderBy`, `SetOrderByDescending`, `SetSkip`, `SetTake`
- `CircuitBreaker` — thread-safe circuit breaker pattern using C# 14 `Lock` class
- `CircuitBreakerOptions` — configurable failure threshold, open duration, and half-open success threshold
- `CircuitState` enum — `Closed`, `Open`, `HalfOpen`

#### Diagnostics
- 8 new OpenTelemetry tag constants: `CorrelationIdTag`, `DurationMsTag`, `EntityTypeTag`, `BatchSizeTag`, `ItemCountTag`, `PageNumberTag`, `PageSizeTag`, `CircuitStateTag`
- EF Core activity tracing via `StartEfActivity` for all new methods (`ef.find.paginated`, `ef.find.spec`, `ef.batch.insert`, `ef.batch.update`, `ef.batch.delete`)

### Changed

#### C# 14 modernization (non-breaking)
- `FunctionalMsSqlDb` — primary constructor, property accessors, collection expressions
- `EfFunctionalDb` — primary constructor, property accessors
- `DapperFunctionalDb` — primary constructor, property accessors, C# 14 `Lock` class

### Notes

- **Zero breaking changes** — all new features are additive; existing public API surface is unchanged
- **Backward compatible** — existing code continues to work without modification
- Minimum runtime: .NET 10 / C# 14

## [1.0.0] - 2025-06-01

### Added
- Initial release
- `FunctionalMsSqlDb` facade with EF Core and Dapper backends
- `EfFunctionalDb` — `GetByIdAsync`, `FindOneAsync`, `QueryAsync`, `AddAsync`, `SaveAsync`, `DeleteByIdAsync`, `CountAsync`, `AnyAsync`, `WithTracking`
- `DapperFunctionalDb` — `QueryAsync`, `QuerySingleAsync`, `ExecuteStoredProcAsync`, `ExecuteStoredProcSingleAsync`, `ExecuteStoredProcNonQueryAsync`
- Transaction support: `InTransactionAsync`, `InTransactionMapAsync`
- `SqlExecutionOptions` with retry and timeout configuration
- `SqlTransientDetector` for transient SQL error detection
- `FunctionalExtensions` — `BindAsync`, `MapAsync`
- OpenTelemetry `ActivitySource` integration
- DI registration via `ServiceCollectionExtensions`
- Full xUnit v3 test suite
