# SharpFunctional.MSSQL - Enhancement Plan & Modernization Roadmap

> **Status:** Planning Phase  
> **Backward Compatibility:** ✅ Guaranteed (all changes are opt-in or non-breaking)  
> **Target Framework:** .NET 10 + C# 14

---

## 📊 Executive Summary

SharpFunctional.MSSQL is a well-designed functional-first database access library with solid architecture. This plan modernizes it to C# 14 standards and adds strategic features without breaking existing code.

**Core Initiatives:**
1. 🔧 **C# 14 Modernization** - Primary constructors, Lock class, pattern matching, collection expressions
2. ✨ **Feature Extensions** - Advanced querying, pagination, circuit breaker, batch operations
3. ⚡ **Performance** - Span<T>, async streams, query result caching
4. 📊 **Diagnostics** - Correlation IDs, activity scoping, performance metrics
5. 🛡️ **Backward Compatibility** - 100% non-breaking, all new features opt-in

---

## Part 1: C# 14 MODERNIZATION (Non-Breaking, High Priority)

### 1.1 Primary Constructors in Domain Classes

**Current State:**
```csharp
public sealed class FunctionalMsSqlDb(
    DbContext? dbContext = null,
    IDbConnection? connection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private readonly DbContext? _ef = dbContext;          // Manual initialization
    private readonly IDbConnection? _connection = connection;
    private readonly SqlExecutionOptions _executionOptions = executionOptions ?? SqlExecutionOptions.Default;
    private readonly ILogger<FunctionalMsSqlDb>? _logger = logger;
```

**Modernization:**
```csharp
// Primary constructors already used correctly
// Enhancement: Use field accessor (C# 14) where appropriate
public sealed class FunctionalMsSqlDb(
    DbContext? dbContext = null,
    IDbConnection? connection = null,
    SqlExecutionOptions? executionOptions = null,
    ILogger<FunctionalMsSqlDb>? logger = null)
{
    private DbContext? Ef => dbContext;
    private IDbConnection? Connection => connection;
    private SqlExecutionOptions ExecutionOptions => executionOptions ?? SqlExecutionOptions.Default;
    private ILogger<FunctionalMsSqlDb>? Logger => logger;
    
    internal IDbTransaction? AmbientTransaction { get; set; }
```

**Target Files:**
- `FunctionalMsSqlDb.cs`
- `EfFunctionalDb.cs`
- `DapperFunctionalDb.cs`
- `SqlExecutionOptions.cs`
- All options classes in `DependencyInjection/`

**Benefits:**
- ✅ Reduced boilerplate
- ✅ Better refactoring support
- ✅ Compiler optimizations
- ✅ Cleaner parameter-to-field mapping

---

### 1.2 Modern Lock Class

**Current:** Legacy `object` locking
```csharp
private readonly object _lock = new object();
lock (_lock) { /* critical section */ }
```

**Modern (C# 14):**
```csharp
private readonly Lock _lock = new();
lock (_lock) { /* critical section */ }
```

**Benefits:**
- 🔒 Type-safe, built-in class
- ⚡ Native compiler optimizations
- 🛡️ Deadlock prevention improvements
- 💾 Zero allocation

**Scan Locations:**
- `SharpFunctionalMsSqlDiagnostics.cs`
- `TransactionExtensions.cs`
- Internal caching mechanisms

---

### 1.3 Collection Expressions

**Current:**
```csharp
var errors = new List<Error> { error1, error2 };
var tags = new[] { "sql", "retry" };
var empty = new List<int>();
```

**Modern:**
```csharp
var errors = [error1, error2];
var tags = ["sql", "retry"];
var empty = [];
```

**Locations:**
- Error collection initialization
- Logging parameter arrays
- SQL command batch collections
- Tag arrays in diagnostics

---

### 1.4 Switch Expressions & Pattern Matching

**Current:**
```csharp
if (dbContext is null) return Option<T>.None;
if (string.IsNullOrWhiteSpace(procName)) return Option<T>.None;

if (state == CircuitState.Open) { /* ... */ }
else if (state == CircuitState.HalfOpen) { /* ... */ }
```

**Modern:**
```csharp
return dbContext switch
{
    not null => Option<T>.Some(/* ... */),
    _ => Option<T>.None
};

return string.IsNullOrWhiteSpace(procName)
    ? Option<T>.None
    : await QueryAsync(...);

var behavior = state switch
{
    CircuitState.Open => RejectOperation(),
    CircuitState.HalfOpen => AllowTestOperation(),
    CircuitState.Closed => AllowNormalOperation(),
    _ => throw new ArgumentOutOfRangeException()
};
```

---

## Part 2: NEW FEATURES (Backward Compatible, Opt-in)

### 2.1 QueryResults<T> Record Type (Medium Priority)

**Purpose:** Standardized pagination and result metadata

```csharp
/// <summary>
/// Represents a query result page with metadata.
/// Supports functional chaining and filtering.
/// </summary>
/// <typeparam name="T">The result item type.</typeparam>
public sealed record QueryResults<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
    
    /// <summary>
    /// Projects items to another type using a selector function.
    /// </summary>
    public QueryResults<TResult> Map<TResult>(Func<T, TResult> selector)
        => new(
            Items.Select(selector).ToList().AsReadOnly(),
            TotalCount,
            PageNumber,
            PageSize);
    
    /// <summary>
    /// Filters results while maintaining pagination metadata.
    /// </summary>
    public QueryResults<T> Where(Func<T, bool> predicate)
        => new(
            Items.Where(predicate).ToList().AsReadOnly(),
            Items.Count(predicate),
            PageNumber,
            PageSize);
}

// API Extensions
public sealed class EfFunctionalDb
{
    /// <summary>
    /// Queries entities with pagination support.
    /// </summary>
    public async Task<Fin<QueryResults<T>>> FindPaginatedAsync<T>(
        Expression<Func<T, bool>> predicate,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        where T : class { /* ... */ }
    
    /// <summary>
    /// Counts total matching entities (used for pagination metadata).
    /// </summary>
    private async Task<int> CountAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        where T : class { /* ... */ }
}

public sealed class DapperFunctionalDb
{
    /// <summary>
    /// Executes a stored procedure with pagination.
    /// </summary>
    public async Task<Fin<QueryResults<T>>> ExecuteStoredProcPaginatedAsync<T>(
        string procName,
        object param,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default) { /* ... */ }
}
```

**File Structure:**
- New: `Common/QueryResults.cs`
- Modified: `Ef/EfFunctionalDb.cs`
- Modified: `Dapper/DapperFunctionalDb.cs`

**Usage Example:**
```csharp
var page = await db.Ef().FindPaginatedAsync<User>(
    u => u.IsActive,
    pageNumber: 2,
    pageSize: 100);

// Functional chaining
var result = page
    .Map(user => new UserDTO { Name = user.Name })
    .Where(dto => dto.Name.StartsWith("A"));
```

---

### 2.2 IQuerySpecification<T> Builder Pattern (Medium Priority)

**Purpose:** Reduce LINQ boilerplate for complex queries

```csharp
/// <summary>
/// Defines a query specification for building type-safe queries.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IQuerySpecification<T>
{
    Expression<Func<T, bool>>? Predicate { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    bool IsDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}

public sealed class QuerySpecification<T>(Expression<Func<T, bool>> predicate)
    : IQuerySpecification<T>
{
    public Expression<Func<T, bool>>? Predicate { get; } = predicate;
    public List<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public bool IsDescending { get; private set; }
    public int? Take { get; private set; }
    public int? Skip { get; private set; }
    
    public QuerySpecification<T> Include(Expression<Func<T, object>> navigationProperty)
    {
        Includes.Add(navigationProperty);
        return this;
    }
    
    public QuerySpecification<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        OrderBy = x => keySelector.Compile()(x);
        IsDescending = true;
        return this;
    }
    
    public QuerySpecification<T> ThenSkip(int count)
    {
        Skip = count;
        return this;
    }
    
    public QuerySpecification<T> ThenTake(int count)
    {
        Take = count;
        return this;
    }
}

// EfFunctionalDb extension
public sealed class EfFunctionalDb
{
    public async Task<Option<IReadOnlyList<T>>> FindAsync<T>(
        IQuerySpecification<T> spec,
        CancellationToken cancellationToken = default)
        where T : class { /* ... */ }
}
```

**File:** New `Common/QuerySpecification.cs`

**Usage:**
```csharp
var spec = new QuerySpecification<User>(u => u.IsActive)
    .Include(u => u.Orders)
    .OrderByDescending(u => u.CreatedAt)
    .ThenSkip(50)
    .ThenTake(25);

var users = await db.Ef().FindAsync(spec, ct);
```

---

### 2.3 Batch Operations Support (Medium Priority)

**Purpose:** Bulk insert/update/delete with functional safety

```csharp
public enum BatchOperationType
{
    Insert,
    Update,
    Delete,
    Upsert
}

public sealed class EfFunctionalDb
{
    /// <summary>
    /// Inserts multiple entities in batches for performance.
    /// </summary>
    public async Task<Fin<int>> InsertBatchAsync<T>(
        IEnumerable<T> items,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var itemList = items.ToList();
            if (itemList.Count == 0) return Fin<int>.Succ(0);
            
            int totalInserted = 0;
            for (int i = 0; i < itemList.Count; i += batchSize)
            {
                var batch = itemList.Skip(i).Take(batchSize);
                await _dbContext.Set<T>().AddRangeAsync(batch);
                totalInserted += await _dbContext.SaveChangesAsync(cancellationToken);
            }
            
            return Fin<int>.Succ(totalInserted);
        }
        catch (Exception ex)
        {
            return FinFail<int>(Error.New(ex.Message, ex));
        }
    }
    
    /// <summary>
    /// Updates multiple entities in batches.
    /// </summary>
    public async Task<Fin<int>> UpdateBatchAsync<T>(
        IEnumerable<T> items,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class { /* Similar */ }
    
    /// <summary>
    /// Deletes multiple entities in batches.
    /// </summary>
    public async Task<Fin<int>> DeleteBatchAsync<T>(
        Expression<Func<T, bool>> predicate,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class { /* Similar */ }
}
```

---

### 2.4 Circuit Breaker Pattern Integration (Low Priority)

**Purpose:** Prevent cascading failures from transient issues

```csharp
public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// Configuration for circuit breaker behavior.
/// </summary>
public sealed class CircuitBreakerOptions
{
    public int FailureThreshold { get; init; } = 5;
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromSeconds(30);
    public int SuccessThresholdInHalfOpen { get; init; } = 2;
}

/// <summary>
/// Implements the circuit breaker pattern for database operations.
/// </summary>
public sealed class CircuitBreaker(
    SqlTransientDetector transientDetector,
    CircuitBreakerOptions? options = null)
{
    private CircuitState _state = CircuitState.Closed;
    private DateTime _openedAt = DateTime.MinValue;
    private int _failureCount = 0;
    private int _successCountInHalfOpen = 0;
    private readonly Lock _stateLock = new();
    private readonly CircuitBreakerOptions _options = options ?? new();
    
    public CircuitState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }
    
    public async Task<Fin<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Fin<T>>> operation,
        CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_state == CircuitState.Open && DateTime.UtcNow - _openedAt < _options.OpenDuration)
                return FinFail<T>(Error.New("Circuit breaker is open"));
            
            if (_state == CircuitState.Open)
                _state = CircuitState.HalfOpen;
        }
        
        var result = await operation(cancellationToken);
        
        lock (_stateLock)
        {
            if (result.IsFail)
            {
                _failureCount++;
                if (_failureCount >= _options.FailureThreshold && _state == CircuitState.Closed)
                {
                    _state = CircuitState.Open;
                    _openedAt = DateTime.UtcNow;
                }
            }
            else
            {
                _failureCount = 0;
                if (_state == CircuitState.HalfOpen)
                {
                    _successCountInHalfOpen++;
                    if (_successCountInHalfOpen >= _options.SuccessThresholdInHalfOpen)
                    {
                        _state = CircuitState.Closed;
                        _successCountInHalfOpen = 0;
                    }
                }
            }
        }
        
        return result;
    }
}

// Integration extension
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFunctionalMsSqlWithCircuitBreaker<TContext>(
        this IServiceCollection services,
        Action<CircuitBreakerOptions>? configureCircuitBreaker = null)
        where TContext : DbContext
    {
        services.AddFunctionalMsSqlEf<TContext>();
        services.Configure(configureCircuitBreaker ?? (o => { }));
        services.AddSingleton<CircuitBreaker>();
        
        return services;
    }
}
```

**File:** New `Common/CircuitBreaker.cs`

---

### 2.5 Async Stream Support (Medium Priority)

**Purpose:** Memory-efficient streaming of large result sets

```csharp
public sealed class EfFunctionalDb
{
    /// <summary>
    /// Streams query results without loading all into memory.
    /// Ideal for large datasets.
    /// </summary>
    public async IAsyncEnumerable<T> StreamAsync<T>(
        Expression<Func<T, bool>> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class
    {
        if (_dbContext is null) yield break;
        
        var query = SetForQuery<T>(_dbContext, _trackingEnabled).Where(predicate);
        await foreach (var item in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}

// Usage
await foreach (var user in db.Ef().StreamAsync<User>(u => u.Status == 1, ct))
{
    await ProcessUserAsync(user);  // No memory explosion
}
```

---

## Part 3: PERFORMANCE OPTIMIZATIONS

### 3.1 Span<T> in Hot Paths
- Use `Span<byte>` for buffer management
- Stack allocation for small arrays: `Span<byte> buffer = stackalloc byte[256];`
- Locations: `SqlTransientDetector`, Dapper parameter binding

### 3.2 ArrayPool<T> for Temporary Buffers
```csharp
var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### 3.3 Query Result Caching (Optional)
```csharp
public interface IQueryCache
{
    Task<Option<T>> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
}

public sealed class CachedEfFunctionalDb
{
    public async Task<Option<T>> GetByIdCachedAsync<T, TId>(
        TId id,
        TimeSpan cacheTtl,
        CancellationToken ct = default)
        where T : class
    {
        var key = $"{typeof(T).Name}:{id}";
        var cached = await _cache.GetAsync<T>(key, ct);
        if (cached.IsSome) return cached;
        
        var fresh = await _innerDb.GetByIdAsync<T, TId>(id, ct);
        if (fresh.IsSome)
            await _cache.SetAsync(key, fresh.Unwrap(), cacheTtl, ct);
        
        return fresh;
    }
}
```

---

## Part 4: DIAGNOSTICS & OBSERVABILITY ENHANCEMENTS

### 4.1 Correlation ID Support
```csharp
public sealed record ActivityContext(
    string CorrelationId,
    string? TraceId = null,
    Dictionary<string, object>? Tags = null);

// Usage in activities
var context = new ActivityContext(
    correlationId: httpContext.TraceIdentifier ?? Guid.NewGuid().ToString());

activity?.SetTag("correlation_id", context.CorrelationId);
activity?.SetTag("trace_id", context.TraceId);
```

### 4.2 Enhanced Activity Scoping
- Automatic transaction activity nesting
- Query duration metrics
- Error categorization
- Connection pool statistics

---

## 📦 VERSIONING STRATEGY

### Version 2.0.0-preview.1
- ✅ C# 14 modernization (internal)
- ✅ QueryResults<T> + pagination
- ✅ IQuerySpecification<T> builder
- 🔄 Community feedback period

### Version 2.0.0
- ✅ Batch operations
- ✅ Circuit breaker
- ✅ Async streaming
- ✅ Full diagnostics

### Backward Compatibility Guarantee
- **Zero breaking changes** to public APIs
- **Drop-in replacement** for existing code
- **Opt-in adoption** for new features
- **Smooth migration path** with examples

---

## 📋 IMPLEMENTATION ROADMAP

| Phase | Feature | Files | Breaking | Effort |
|-------|---------|-------|----------|--------|
| 1 | Primary Constructors | 5 | ❌ No | 1 day |
| 1 | Lock → Lock class | 3 | ❌ No | 0.5 day |
| 2 | Collection Expressions | 8+ | ❌ No | 1 day |
| 2 | Switch Expressions | 6+ | ❌ No | 0.5 day |
| 2 | QueryResults<T> | 3 | ❌ No | 1.5 days |
| 3 | QuerySpecification | 2 | ❌ No | 1 day |
| 3 | Batch Operations | 2 | ❌ No | 1.5 days |
| 4 | Circuit Breaker | 1 | ❌ No | 1.5 days |
| 4 | Async Streaming | 2 | ❌ No | 1 day |
| 5 | Testing & Docs | 5+ | ❌ No | 2 days |

**Total Estimated Effort:** ~11 days (spread over 4 weeks)

---

## 🎯 SUCCESS CRITERIA

- ✅ All 12 steps completed
- ✅ 0 breaking changes verified
- ✅ Test coverage ≥95%
- ✅ Backward compatibility confirmed
- ✅ Performance benchmarks pass
- ✅ Documentation updated
- ✅ Migration guide available

---

## 🚀 QUICK WIN PRIORITIES

**High Priority (Start Here):**
1. Primary constructors modernization
2. Collection expressions
3. QueryResults<T> pagination

**Medium Priority:**
4. QuerySpecification builder
5. Batch operations
6. Async streaming

**Nice-to-Have:**
7. Circuit breaker integration
8. Advanced caching layer

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-28  
**Status:** Ready for Implementation Planning
