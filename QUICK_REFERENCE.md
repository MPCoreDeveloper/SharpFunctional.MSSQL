# C# 14 Modernization Quick Reference Card

**Print this and put on your desk!** 🖨️

---

## Pattern 1: Primary Constructors

```csharp
// ❌ OLD WAY
public class Foo(string name, int value)
{
    private readonly string _name = name;
    private readonly int _value = value;
}

// ✅ MODERN WAY (C# 14)
public class Foo(string name, int value)
{
    private string Name => name;
    private int Value => value;
    // Parameters available directly!
}
```

---

## Pattern 2: Lock Class

```csharp
// ❌ OLD WAY (Unsafe, type-less)
private readonly object _lock = new object();
lock (_lock) { /* ... */ }

// ✅ MODERN WAY (C# 14)
private readonly Lock _lock = new();
lock (_lock) { /* ... */ }
```

---

## Pattern 3: Collection Expressions

```csharp
// ❌ OLD WAY
var list = new List<int> { 1, 2, 3 };
var array = new[] { "a", "b" };
var empty = new List<string>();

// ✅ MODERN WAY (C# 14)
var list = [1, 2, 3];
var array = ["a", "b"];
var empty = [];
```

---

## Pattern 4: Switch Expressions

```csharp
// ❌ OLD WAY
if (value == 1) return "One";
else if (value == 2) return "Two";
else return "Other";

// ✅ MODERN WAY (C# 14)
return value switch
{
    1 => "One",
    2 => "Two",
    _ => "Other"
};
```

---

## Pattern 5: Null Patterns

```csharp
// ❌ OLD WAY
if (obj != null)
    DoSomething(obj);
else
    DoOtherThing();

// ✅ MODERN WAY (C# 14)
DoSomething(obj) ?? DoOtherThing();

// Or as switch
_ = obj switch
{
    not null => DoSomething(obj),
    null => DoOtherThing()
};
```

---

## Feature 1: QueryResults<T>

```csharp
// USAGE
var page = await db.Ef().FindPaginatedAsync<User>(
    u => u.Active,
    pageNumber: 2,
    pageSize: 50);

if (page.IsSucc)
{
    var results = page.Unwrap();
    Console.WriteLine($"Page {results.PageNumber}/{results.TotalPages}");
    
    var dtos = results.Map(u => new UserDTO { ... });
}
```

---

## Feature 2: QuerySpecification<T>

```csharp
// USAGE
var spec = new QuerySpecification<User>(u => u.Active)
    .Include(u => u.Orders)
    .OrderByDescending(u => u.CreatedAt)
    .ThenSkip(50)
    .ThenTake(25);

var users = await db.Ef().FindAsync(spec);
```

---

## Feature 3: Batch Operations

```csharp
// BEFORE: Slow (10,000 round trips)
foreach (var user in users)
{
    await db.Ef().AddAsync(user);
    await db.Ef().SaveAsync();
}

// AFTER: Fast (10 round trips)
await db.Ef().InsertBatchAsync(users, batchSize: 1000);
```

---

## Feature 4: Async Streaming

```csharp
// BEFORE: OOM (all in memory)
var users = await db.Ef().FindAllAsync<User>();
foreach (var user in users) { /* ... */ }

// AFTER: Efficient (streaming)
await foreach (var user in db.Ef().StreamAsync<User>(u => true))
{
    // Process one at a time
}
```

---

## Checklist Before Commit

- [ ] Used primary constructors where appropriate
- [ ] No `object` locks (use `Lock` class)
- [ ] Collection expressions used (`[...]` not `new List`)
- [ ] Switch expressions used (not if-else chains)
- [ ] Null patterns used (`not null`, `switch`)
- [ ] All async methods end with `Async`
- [ ] All methods accept `CancellationToken` where applicable
- [ ] No sync-over-async patterns
- [ ] Tests pass: `dotnet test`
- [ ] Build passes: `dotnet build`

---

## Performance Tips

| Scenario | Use This | Why |
|----------|----------|-----|
| 1000+ inserts | `InsertBatchAsync()` | 1000x faster |
| 1M+ records | `StreamAsync()` | No OOM |
| Pagination | `FindPaginatedAsync()` | No N+1 queries |
| Complex queries | `QuerySpecification<T>` | Readable builder |
| Temporary buffer | `ArrayPool<T>.Shared.Rent()` | Zero allocation |
| Small stack buffer | `stackalloc byte[256]` | No heap |

---

## Common Gotchas

### ❌ Don't do this:
```csharp
public async void HandleClick() { }           // No! Use Task
public async Task Load() { return loader.LoadAsync().Result; }  // Deadlock!
lock (new object()) { }                       // New object each time!
var users = new List<User>(users.Count);     // Use collection expressions
```

### ✅ Do this instead:
```csharp
public async Task HandleClickAsync() { }     // Task return
public async Task LoadAsync() { await loader.LoadAsync(); }  // Async all the way
private readonly Lock _lock = new();          // Reusable Lock instance
var users = users.ToList();                  // Or [user1, user2, ...]
```

---

## CI/CD Pre-Flight Checks

Run before pushing:

```powershell
# Format check
dotnet format --verify-no-changes

# Build
dotnet build --configuration Release

# Test
dotnet test --collect:"XPlat Code Coverage"

# Coverage > 95%?
dotnet-coverage view coverage.cobertura.xml
```

---

## Links to Full Docs

| Document | Purpose |
|----------|---------|
| `ENHANCEMENT_PLAN.md` | Full technical plan |
| `CODE_EXAMPLES.md` | Before/after code |
| `IMPLEMENTATION_CHECKLIST.md` | Phase-by-phase guide |
| `PLAN_SUMMARY.md` | Executive overview |

---

## Quick Reference: New Methods

| Class | Method | Purpose |
|-------|--------|---------|
| `EfFunctionalDb` | `FindPaginatedAsync<T>()` | Paginated queries |
| `EfFunctionalDb` | `FindAsync<T>(IQuerySpecification)` | Builder queries |
| `EfFunctionalDb` | `InsertBatchAsync<T>()` | Bulk insert |
| `EfFunctionalDb` | `UpdateBatchAsync<T>()` | Bulk update |
| `EfFunctionalDb` | `DeleteBatchAsync<T>()` | Bulk delete |
| `EfFunctionalDb` | `StreamAsync<T>()` | Stream results |
| `DapperFunctionalDb` | `ExecuteStoredProcPaginatedAsync<T>()` | Paginated SP |

---

## Backward Compatibility

✅ **GUARANTEED**: All new features are opt-in  
✅ **GUARANTEED**: Existing APIs unchanged  
✅ **GUARANTEED**: Drop-in replacement  

Your existing code will work without modification!

---

**Version:** 1.0 | **Updated:** 2025-01-28 | **Status:** Ready
