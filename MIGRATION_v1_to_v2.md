# Migration Guide: v1.0.0 → v2.0.0-preview.1

## Overview

**SharpFunctional.MSSQL v2.0.0-preview.1** is a **fully backward-compatible** release.
No breaking changes were introduced — your existing code will continue to work without modification.

This guide shows how to adopt the new features.

---

## Prerequisites

- .NET 10 SDK
- C# 14 language version

---

## What changed (non-breaking)

| Area | Change | Impact |
|------|--------|--------|
| Core files | Primary constructors, property accessors, collection expressions | Internal-only; no API change |
| Lock class | `object` locks replaced with C# 14 `Lock` | Internal-only; thread-safety improved |
| Diagnostics | 8 new tag constants added to `SharpFunctionalMsSqlDiagnostics` | Additive; existing tags unchanged |
| EF Core | 6 new methods added to `EfFunctionalDb` | Additive; existing methods unchanged |
| Dapper | 1 new method added to `DapperFunctionalDb` | Additive; existing methods unchanged |
| Common | 3 new types: `QueryResults<T>`, `QuerySpecification<T>`, `CircuitBreaker` | New types; no impact on existing code |

---

## Adopting new features

### 1. Paginated queries

**Before (v1):** Manual Skip/Take with separate count query.

```csharp
// v1 — manual pagination
var allItems = await db.Ef().QueryAsync<User>(u => u.IsActive, ct);
var count = await db.Ef().CountAsync<User>(u => u.IsActive, ct);
var page = allItems.Skip(50).Take(25);
```

**After (v2):** Single call with server-side pagination.

```csharp
// v2 — built-in pagination
var result = await db.Ef().FindPaginatedAsync<User>(
    u => u.IsActive,
    pageNumber: 3,
    pageSize: 25,
    ct);

result.Match(
    Succ: page => Console.WriteLine($"Page {page.PageNumber}/{page.TotalPages}"),
    Fail: error => Console.WriteLine(error));
```

### 2. Specification pattern

**Before (v1):** Inline predicates repeated across services.

```csharp
// v1 — inline predicate
var users = await db.Ef().QueryAsync<User>(
    u => u.IsActive && u.CreatedDate > cutoff, ct);
```

**After (v2):** Reusable, composable specification.

```csharp
// v2 — specification pattern
var spec = new QuerySpecification<User>(u => u.IsActive && u.CreatedDate > cutoff)
    .SetOrderByDescending(u => u.CreatedDate)
    .SetTake(100);

var users = await db.Ef().FindAsync(spec, ct);
```

### 3. Batch operations

**Before (v1):** Individual `AddAsync` + `SaveAsync` calls in a loop.

```csharp
// v1 — one at a time
foreach (var user in newUsers)
{
    await db.Ef().AddAsync(user, ct);
}
```

**After (v2):** Batch insert/update/delete with configurable batch size.

```csharp
// v2 — batch insert
var inserted = await db.Ef().InsertBatchAsync(newUsers, batchSize: 100, ct);

// v2 — batch update (tracked entities)
var updated = await db.Ef().WithTracking().UpdateBatchAsync(modifiedUsers, batchSize: 100, ct);

// v2 — batch delete by predicate
var deleted = await db.Ef().DeleteBatchAsync<User>(u => !u.IsActive, batchSize: 200, ct);
```

### 4. Streaming

**Before (v1):** Materialize entire result set.

```csharp
// v1 — full materialization
var allUsers = await db.Ef().QueryAsync<User>(u => u.IsActive, ct);
foreach (var user in allUsers) { /* process */ }
```

**After (v2):** Stream with `IAsyncEnumerable<T>`.

```csharp
// v2 — streaming (constant memory)
await foreach (var user in db.Ef().StreamAsync<User>(u => u.IsActive, ct))
{
    await ProcessUserAsync(user, ct);
}
```

### 5. Circuit breaker

**New in v2** — wrap database calls with circuit breaker protection.

```csharp
var breaker = new CircuitBreaker(new CircuitBreakerOptions
{
    FailureThreshold = 5,
    OpenDuration = TimeSpan.FromSeconds(30),
    SuccessThresholdInHalfOpen = 2
});

var result = await breaker.ExecuteAsync(
    async ct => await db.Ef().GetByIdAsync<User, int>(42, ct),
    ct);
```

### 6. Dapper paginated stored procedures

**New in v2** — execute stored procedures that return paginated result sets.

```csharp
var page = await db.Dapper().ExecuteStoredProcPaginatedAsync<OrderDto>(
    "usp_GetOrders",
    new { StatusId = 1, PageNumber = 1, PageSize = 50 },
    ct);
```

> **Requirement:** The stored procedure must return two result sets: page items first, then a scalar total count.

### 7. Extended OpenTelemetry tags

If you filter or alert on OpenTelemetry tags, the following new tag keys are available:

| Tag constant | Key |
|---|---|
| `EntityTypeTag` | `sharpfunctional.mssql.entity_type` |
| `BatchSizeTag` | `sharpfunctional.mssql.batch_size` |
| `ItemCountTag` | `sharpfunctional.mssql.item_count` |
| `PageNumberTag` | `sharpfunctional.mssql.page_number` |
| `PageSizeTag` | `sharpfunctional.mssql.page_size` |
| `DurationMsTag` | `sharpfunctional.mssql.duration_ms` |
| `CorrelationIdTag` | `sharpfunctional.mssql.correlation_id` |
| `CircuitStateTag` | `sharpfunctional.mssql.circuit_state` |

---

## NuGet update

```bash
dotnet add package SharpFunctional.MsSql --version 2.0.0-preview.1
```

---

## Questions?

Open an issue at [GitHub](https://github.com/MPCoreDeveloper/SharpFunctional.MSSQL/issues).
