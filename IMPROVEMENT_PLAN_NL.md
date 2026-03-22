# SharpFunctional.MSSQL - Verbeterplan & Modernisatiegroep

> **Status:** Planning Phase  
> **Backward Compatibility:** ✅ Gegarandeerd (alle wijzigingen zijn opt-in of non-breaking)  
> **Target:** .NET 10 + C# 14

---

## 📊 Executive Summary

SharpFunctional.MSSQL is een goed ontworpen functional-first library met solide architectuur. Dit plan moderniseert het naar C# 14 standaarden en voegt strategische features toe zonder breaking changes.

**Kernideeën:**
1. 🔧 **C# 14 Modernisering** - Primary constructors, Lock klasse, pattern matching
2. ✨ **Feature Extensions** - QueryBuilder, pagination, circuit breaker
3. ⚡ **Performance** - Span<T>, caching, diagnostics
4. 🛡️ **Backward Compatibility** - 100% non-breaking (optionele features)

---

## Part 1: C# 14 MODERNISERING (Non-Breaking)

### 1.1 Primary Constructors
**Huidge Stand:** Manuele veld-initialisatie in body
```csharp
// OLD (huident)
public sealed class FunctionalMsSqlDb(
    DbContext? dbContext = null,
    IDbConnection? connection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private readonly DbContext? _ef = dbContext;          // ← Manual init
    private readonly IDbConnection? _connection = connection;
    private readonly SqlExecutionOptions _executionOptions = executionOptions ?? SqlExecutionOptions.Default;
    private readonly ILogger<FunctionalMsSqlDb>? _logger = logger;
```

**Modernisering:** Verspil `field` accessor (C# 14)
```csharp
// MODERN (C# 14)
public sealed class FunctionalMsSqlDb(
    DbContext? dbContext = null,
    IDbConnection? connection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private DbContext? Ef { get; } = dbContext;
    private IDbConnection? Connection { get; } = connection;
    private SqlExecutionOptions ExecutionOptions { get; } = executionOptions ?? SqlExecutionOptions.Default;
    private ILogger<FunctionalMsSqlDb>? Logger { get; } = logger;
    
    internal IDbTransaction? AmbientTransaction { get; set; }
```

**Voordelen:**
- ✅ Minder boilerplate
- ✅ Direct veldnamen in parameters zichtbaar
- ✅ Beter refactoring support
- ✅ Performance: compiler optimalisaties

**Betreffende files:**
- `FunctionalMsSqlDb.cs`
- `EfFunctionalDb.cs`
- `DapperFunctionalDb.cs`
- `FunctionalMsSqlDbOptions.cs`
- `SqlExecutionOptions.cs`

---

### 1.2 Lock Klasse (Thread-Safety Moderne)
**Huidig:** Oude `object` lock pattern
```csharp
// OLD - Anti-pattern in moderne C#
private readonly object _lock = new object();
lock (_lock) { /* ... */ }
```

**Modernisering:** C# 14 `Lock` klasse
```csharp
// MODERN - C# 14 built-in
private readonly Lock _lock = new();
lock (_lock) { /* ... */ }
```

**Impact:** 
- 🔒 Type-safe lock
- ⚡ Native compiler optimalisaties
- 🛡️ Deadlock-preventie improvements
- ✅ Zero allocation

**Zoeken naar:** grep voor `object.*lock` patterns in:
- `SharpFunctionalMsSqlDiagnostics.cs`
- `TransactionExtensions.cs`
- Enige internal caches

---

### 1.3 Collection Expressions
**Huidig:** Oude array/list syntaxis
```csharp
// OLD
var errors = new List<Error> { error1, error2 };
var tags = new[] { "tag1", "tag2" };
```

**Modernisering:** Collection literals
```csharp
// MODERN - C# 14
var errors = [error1, error2];
var tags = ["tag1", "tag2"];
var empty = [];
```

**Locaties:**
- Alle ERROR collection initializations
- Alle logging parameter arrays
- SQL command batches

---

### 1.4 Switch Expressions & Pattern Matching
**Huidig:** If-else chains
```csharp
// OLD
if (option.IsNone) return FinFail(...);
if (string.IsNullOrWhiteSpace(procName)) return None;
```

**Modernisering:** Switch expressions
```csharp
// MODERN - C# 14 pattern matching
return option switch
{
    not null => Ok(option),
    _ => FinFail(...)
};

return string.IsNullOrWhiteSpace(procName) 
    ? Option<T>.None 
    : await QueryAsync(...);
```

---

## Part 2: NIEUWSE FEATURES (Backward Compatible)

### 2.1 QueryResults<T> Record Type
**Voordeel:** Standaardized result wrapping met metadata

```csharp
/// <summary>
/// Represents a query result with pagination and metadata.
/// </summary>
/// <typeparam name="T">The result item type.</typeparam>
public sealed record QueryResults<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    
    /// <summary>
    /// Indicates if more items exist.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
    
    /// <summary>
    /// Projects items to another type.
    /// </summary>
    public QueryResults<TResult> Map<TResult>(Func<T, TResult> selector)
        => new(Items.Select(selector).ToList(), TotalCount, PageNumber, PageSize);
}
```

**API Extensies:**
```csharp
// EfFunctionalDb.cs
public async Task<Option<QueryResults<T>>> FindPaginatedAsync<T>(
    Expression<Func<T, bool>> predicate,
    int pageNumber = 1,
    int pageSize = 50,
    CancellationToken ct = default)
    where T : class { }

// DapperFunctionalDb.cs
public async Task<Option<QueryResults<T>>> ExecuteStoredProcPaginatedAsync<T>(
    string procName,
    object param,
    int pageNumber = 1,
    int pageSize = 50,
    CancellationToken ct = default) { }
```

**Backward Compatibility:** Opt-in method, bestaande APIs onveranderd

---

### 2.2 IQuerySpecification<T> Builder Pattern
**Use case:** Complex query building zonder LINQ bloat

```csharp
public interface IQuerySpecification<T>
{
    Expression<Func<T, bool>>? Predicate { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    bool IsDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}

public class QuerySpecification<T> : IQuerySpecification<T>
{
    public QuerySpecification(Expression<Func<T, bool>> predicate)
        => Predicate = predicate;
    
    public QuerySpecification<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector, bool descending = false)
        => /* ... */;
    
    public QuerySpecification<T> Skip(int count)
        => /* ... */;
    
    public QuerySpecification<T> Take(int count)
        => /* ... */;
}

// Usage:
var spec = new QuerySpecification<User>(u => u.Active)
    .OrderBy(u => u.CreatedAt, descending: true)
    .Skip(50)
    .Take(25);
    
await db.Ef().FindAsync(spec, ct);
```

**Files:** Maak `Common/QuerySpecification.cs`

---

### 2.3 Async Batch Operations
**Use case:** Bulk inserts/updates met functional safety

```csharp
public sealed record BatchOperation<T>(
    IEnumerable<T> Items,
    BatchOperationType OperationType,
    int BatchSize = 1000);

public enum BatchOperationType
{
    Insert,
    Update,
    Delete,
    Upsert
}

// EfFunctionalDb.cs
public async Task<Fin<int>> InsertBatchAsync<T>(
    IEnumerable<T> items,
    int batchSize = 1000,
    CancellationToken ct = default)
    where T : class { }

public async Task<Fin<int>> UpdateBatchAsync<T>(
    IEnumerable<T> items,
    int batchSize = 1000,
    CancellationToken ct = default)
    where T : class { }
```

---

### 2.4 Circuit Breaker Pattern (Optioneel)
**Gebouwd op:** Bestaande `SqlTransientDetector`

```csharp
public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; init; } = 5;
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromSeconds(30);
    public int SuccessThresholdInHalfOpen { get; init; } = 2;
}

public sealed class CircuitBreaker
{
    public CircuitState State { get; private set; }
    
    public async Task<Fin<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Fin<T>>> operation,
        CancellationToken ct = default)
    {
        if (State == CircuitState.Open && !IsHalfOpenTime())
            return FinFail<T>(Error.New("Circuit breaker is open"));
        
        /* Execute and track */
    }
}

// Usage:
var breaker = new CircuitBreaker(new CircuitBreakerOptions());
var result = await breaker.ExecuteAsync(
    ct => db.Dapper().ExecuteStoredProcSingleAsync<User>("GetUser", new { id }, ct),
    ct);
```

**Integration:**
- Meld aan bestaande `ExecuteWithRetryAsync` hook
- Geen wijzigingen aan bestaande APIs
- Opt-in via ServiceCollection extension

---

### 2.5 Query Caching Layer (Optioneel)
**Strategie:** Optional distributed cache support

```csharp
public interface IQueryCache
{
    Task<Option<T>> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken ct = default);
    Task InvalidateAsync(string pattern, CancellationToken ct = default);
}

// EfFunctionalDb.cs
public class EfFunctionalDbWithCache
{
    public async Task<Option<T>> GetByIdCachedAsync<T, TId>(
        TId id,
        TimeSpan cacheDuration,
        CancellationToken ct = default)
        where T : class
    {
        var cacheKey = $"{typeof(T).Name}:{id}";
        
        var cached = await _cache.GetAsync<T>(cacheKey, ct);
        if (cached.IsSome) return cached;
        
        var result = await GetByIdAsync<T, TId>(id, ct);
        if (result.IsSome)
            await _cache.SetAsync(cacheKey, result.Unwrap(), cacheDuration, ct);
        
        return result;
    }
}
```

**Backward Compatible:** Separate class, geen wijzigingen aan basis APIs

---

## Part 3: PERFORMANCE OPTIMALISATIES

### 3.1 Span<T> in Hot Paths
**Locaties:**
- `BuildPrimaryKeyPredicate` - use Span for paramter arrays
- Error collection handling - Span for logging
- Buffer management in Dapper commands

### 3.2 ArrayPool<T> voor Temporaire Buffers
```csharp
// Old
var buffer = new byte[bufferSize];

// Modern
var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
try { /* use buffer */ }
finally { ArrayPool<byte>.Shared.Return(buffer); }
```

### 3.3 Async Stream Operators
```csharp
// Add to EfFunctionalDb
public async IAsyncEnumerable<T> QueryAsyncEnumerable<T>(
    Expression<Func<T, bool>> predicate,
    [EnumeratorCancellation] CancellationToken ct = default)
    where T : class
{
    var query = SetForQuery<T>(_dbContext, _trackingEnabled).Where(predicate);
    await foreach (var item in query.AsAsyncEnumerable().WithCancellation(ct))
    {
        yield return item;
    }
}

// Usage - streaming large result sets
await foreach (var user in db.Ef().QueryAsyncEnumerable<User>(u => u.Status == 1, ct))
{
    // Process without loading all in memory
}
```

---

## Part 4: DIAGNOSTICS & OBSERVABILITY

### 4.1 Correlation IDs
```csharp
public sealed record ActivityContext(
    string CorrelationId,
    string? TraceId = null,
    Dictionary<string, object>? Tags = null);

// Integration in activities
var context = new ActivityContext(
    correlationId: HttpContext.TraceIdentifier,
    traceId: Activity.Current?.Id);

activity?.SetTag("correlation_id", context.CorrelationId);
```

### 4.2 Activity Scoping Improvements
- Automatische nesting van transactions
- Query duration metrics
- Error categorization tags
- Connection pool statistics

---

## Part 5: TESTING IMPROVEMENTS

### 5.1 Test Helpers
```csharp
public static class EfTestHelpers
{
    public static async Task<Fin<T>> ExecuteInContextAsync<T, TContext>(
        Func<TContext, Task<Fin<T>>> action,
        string connectionString)
        where TContext : DbContext, new()
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString)
            .Options;
        
        using var context = new TContext(options);
        return await action(context);
    }
}
```

### 5.2 Xunit Parameterized Tests
```csharp
[Theory]
[InlineData(1, "SingleId")]
[InlineData(null, "NullId")]
[InlineData(0, "ZeroId")]
public async Task GetByIdAsync_WithVariousIds_ReturnsExpectedResult(int? id, string scenario)
{
    // Arrange, Act, Assert
}
```

---

## 📦 VERSIONING STRATEGIE

### Version 2.0.0-preview.1
- ✅ C# 14 modernisering (internal only)
- ✅ QueryResults<T> + pagination
- ✅ IQuerySpecification<T> builder
- 🔄 Polling period for feedback

### Version 2.0.0
- ✅ Batch operations
- ✅ Circuit breaker integration
- ✅ Query caching layer
- ✅ Full diagnostics

### Backward Compatibility
- **Guaranteed:** All new features are additive
- **Zero breaking changes** to existing public APIs
- **Opt-in adoption** for all new capabilities
- **Drop-in replacement** for existing code

---

## 📋 IMPLEMENTATIE ROADMAP

| Phase | Feature | Files | Breaking? | Priority |
|-------|---------|-------|-----------|----------|
| 1 | Primary Constructors | 5 files | ❌ No | 🟢 High |
| 1 | Lock → Lock klasse | 3 files | ❌ No | 🟢 High |
| 2 | Collection Expressions | 8+ files | ❌ No | 🟡 Medium |
| 2 | QueryResults<T> | +1 file | ❌ No | 🟡 Medium |
| 3 | IQuerySpecification | +2 files | ❌ No | 🟡 Medium |
| 3 | Batch Operations | +2 files | ❌ No | 🔴 Low |
| 4 | Circuit Breaker | +1 file | ❌ No | 🔴 Low |
| 4 | Query Caching | +2 files | ❌ No | 🔴 Low |

---

## 🎯 SUCCESS CRITERIA

- ✅ 0 breaking changes
- ✅ 95%+ test coverage maintained
- ✅ Backward compatibility verified
- ✅ Performance benchmarks green
- ✅ Documentation updated
- ✅ Migration guide provided

---

## 📚 NEXT STEPS

1. **Phase 1 Audit:** Review huidden codebase voor C# 14 opportunities
2. **Phase 2 Implementation:** Primary constructors + Lock modernisering
3. **Phase 3 Testing:** Unit tests + integration tests
4. **Phase 4 Release:** Preview version publiceren
5. **Phase 5 Feedback:** Community feedback verzamelen
6. **Phase 6 Final:** GA release v2.0.0

---

**Document Version:** 1.0  
**Laatst Updated:** 2025-01-28  
**Status:** Ready for Implementation
