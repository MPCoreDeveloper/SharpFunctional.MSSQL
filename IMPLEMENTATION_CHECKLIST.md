# SharpFunctional.MSSQL - Technical Implementation Checklist

> **Purpose:** Step-by-step verification checklist for modernization  
> **Status:** Ready for Phase 1 kickoff  
> **Target Branches:** feature/* → development → main

---

## 🔍 Phase 1: Code Audit & C# 14 Foundation

### 1.1 Primary Constructor Audit
- [ ] Scan all classes with manual field initialization
- [ ] Document constructor parameter-to-field mappings
- [ ] Identify candidates for `field` accessor (C# 14)
- [ ] Check for complex initialization logic (if-else, ternary)

**Files to Review:**
- [ ] `FunctionalMsSqlDb.cs`
- [ ] `EfFunctionalDb.cs`
- [ ] `DapperFunctionalDb.cs`
- [ ] `SqlExecutionOptions.cs`
- [ ] `FunctionalMsSqlDbOptions.cs`
- [ ] All classes in `DependencyInjection/`

### 1.2 Lock Statement Audit
- [ ] Search entire codebase for `object.*lock` patterns
- [ ] Identify thread-synchronization locations
- [ ] Document lock scope and criticality
- [ ] Prepare Lock class migration plan

**Search Pattern:** `private.*readonly.*object.*lock` OR `lock\s*\(.*object`

**Files to Review:**
- [ ] `SharpFunctionalMsSqlDiagnostics.cs`
- [ ] `TransactionExtensions.cs`
- [ ] All internal cache implementations

### 1.3 Collection Expression Audit
- [ ] Scan for `new List<T> { ... }` patterns
- [ ] Scan for `new[] { ... }` array initializations
- [ ] Scan for `new List<T>()` empty lists
- [ ] Document all occurrences with line numbers

**Search Pattern:** `new\s+(List|Dictionary|Array)\s*<` OR `new\[\]`

### 1.4 Switch/Pattern Matching Audit
- [ ] Identify if-else chains that could be switch expressions
- [ ] Document null-checking patterns
- [ ] Identify enum comparisons
- [ ] List candidates for pattern matching

### 1.5 Null Handling Audit
- [ ] Verify all `ArgumentNullException.ThrowIfNull()` usage
- [ ] Check `string.IsNullOrWhiteSpace()` patterns
- [ ] Identify redundant null checks
- [ ] Document edge cases

---

## 📝 Phase 2: Modernization Implementation

### 2.1 Primary Constructor Refactoring
**Branch:** `feature/modernize-primary-constructors`

- [ ] Update `FunctionalMsSqlDb.cs`
  - [ ] Remove manual field initialization
  - [ ] Use parameter fields directly via properties
  - [ ] Update all references to use new property names
  - [ ] Verify transaction handling still works
  - [ ] Run tests

- [ ] Update `EfFunctionalDb.cs`
  - [ ] Simplify constructor body
  - [ ] Remove duplicate initialization
  - [ ] Verify tracking behavior preserved
  - [ ] Run tests

- [ ] Update `DapperFunctionalDb.cs`
  - [ ] Remove manual field assignments
  - [ ] Verify connection handling
  - [ ] Run tests

- [ ] Update `SqlExecutionOptions.cs`
  - [ ] Verify validation logic preserved
  - [ ] Run tests

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] No behavioral changes
- [ ] Code review approved

### 2.2 Lock Class Migration
**Branch:** `feature/modernize-locks`

- [ ] Identify all lock statement locations
- [ ] Create Lock fields: `private readonly Lock _lock = new();`
- [ ] Update lock statements: `lock (_lock) { }`
- [ ] Remove old `object` lock fields
- [ ] Stress test thread-safety (load tests)

**Files to Update:**
- [ ] `SharpFunctionalMsSqlDiagnostics.cs`
- [ ] `TransactionExtensions.cs`
- [ ] Any internal caches

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] Concurrency tests pass (thread-safety)
- [ ] Load tests show no regression
- [ ] Code review approved

### 2.3 Collection Expressions
**Branch:** `feature/collection-expressions`

- [ ] Update error collections: `[error1, error2]`
- [ ] Update logging arrays: `["tag1", "tag2"]`
- [ ] Update empty collections: `[]`
- [ ] Update tag arrays in diagnostics
- [ ] Update test data initialization

**Priority Files:**
- [ ] All files with `new List<Error>`
- [ ] All files with `new[] { ... }`
- [ ] Logging-related code
- [ ] Diagnostics classes

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] No behavioral changes
- [ ] Code review approved

### 2.4 Switch Expressions & Pattern Matching
**Branch:** `feature/modern-patterns`

- [ ] Replace null checks with null-coalescing patterns
- [ ] Convert if-else chains to switch expressions
- [ ] Use pattern matching for enum checks
- [ ] Apply discards (`_`) for unused values

**Example Conversions:**
```csharp
// OLD
if (option.IsNone) return FinFail(...);
else return Ok(option);

// NEW
return option.IsNone
    ? FinFail(...)
    : Ok(option);

// OR switch expression
return option switch
{
    not null => Ok(option),
    _ => FinFail(...)
};
```

**Files to Update:**
- [ ] `FunctionalMsSqlDb.cs`
- [ ] `EfFunctionalDb.cs`
- [ ] `DapperFunctionalDb.cs`
- [ ] `FunctionalExtensions.cs`
- [ ] All validation methods

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] Code is more readable
- [ ] Code review approved

---

## ✨ Phase 3: New Features Implementation

### 3.1 QueryResults<T> Record Type
**Branch:** `feature/query-results-pagination`

- [ ] Create `Common/QueryResults.cs`
  - [ ] Define record structure
  - [ ] Implement `Map<TResult>()`
  - [ ] Implement `Where()` predicate
  - [ ] Add XML documentation

- [ ] Update `EfFunctionalDb.cs`
  - [ ] Add `FindPaginatedAsync<T>()`
  - [ ] Add private `CountAsync<T>()`
  - [ ] Add tests

- [ ] Update `DapperFunctionalDb.cs`
  - [ ] Add `ExecuteStoredProcPaginatedAsync<T>()`
  - [ ] Add tests

**Tests:**
- [ ] Pagination boundary conditions (page 1, last page, beyond)
- [ ] Item count validation
- [ ] Map projection
- [ ] Where filtering

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All new tests pass
- [ ] Existing tests pass
- [ ] Backward compatibility verified
- [ ] Code review approved

### 3.2 QuerySpecification<T> Builder
**Branch:** `feature/query-specification-builder`

- [ ] Create `Common/QuerySpecification.cs`
  - [ ] Define interface `IQuerySpecification<T>`
  - [ ] Implement `QuerySpecification<T>` class
  - [ ] Implement fluent builder methods
  - [ ] Add XML documentation

- [ ] Update `EfFunctionalDb.cs`
  - [ ] Add `FindAsync<T>(IQuerySpecification<T>)`
  - [ ] Support Include() navigation properties
  - [ ] Support OrderBy/OrderByDescending
  - [ ] Support Skip/Take paging
  - [ ] Add tests

**Tests:**
- [ ] Basic predicate
- [ ] Include navigation properties
- [ ] Ordering ascending/descending
- [ ] Skip/Take pagination
- [ ] Combined specifications

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All new tests pass
- [ ] No regression in existing tests
- [ ] Code review approved

### 3.3 Batch Operations
**Branch:** `feature/batch-operations`

- [ ] Update `EfFunctionalDb.cs`
  - [ ] Add `InsertBatchAsync<T>()`
  - [ ] Add `UpdateBatchAsync<T>()`
  - [ ] Add `DeleteBatchAsync<T>()`
  - [ ] Implement batch processing with configurable size
  - [ ] Add transaction support
  - [ ] Add tests

**Tests:**
- [ ] Insert 0 items
- [ ] Insert 1 item
- [ ] Insert 1000 items (multiple batches)
- [ ] Update with transaction
- [ ] Delete with predicate
- [ ] Error handling

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] Performance acceptable (benchmark)
- [ ] Transaction semantics preserved
- [ ] Code review approved

### 3.4 Async Streaming
**Branch:** `feature/async-streaming`

- [ ] Update `EfFunctionalDb.cs`
  - [ ] Add `StreamAsync<T>(Expression<...>)`
  - [ ] Use `AsAsyncEnumerable()` for streaming
  - [ ] Support cancellation token
  - [ ] Add XML documentation
  - [ ] Add tests

**Tests:**
- [ ] Empty result stream
- [ ] Small stream (< 10 items)
- [ ] Large stream (1000+ items)
- [ ] Cancellation support
- [ ] Exception handling

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] Memory usage reasonable for large datasets
- [ ] Code review approved

### 3.5 Circuit Breaker (Optional, Low Priority)
**Branch:** `feature/circuit-breaker`

- [ ] Create `Common/CircuitBreaker.cs`
  - [ ] Define `CircuitState` enum
  - [ ] Define `CircuitBreakerOptions`
  - [ ] Implement `CircuitBreaker` class
  - [ ] Add state machine logic
  - [ ] Add XML documentation

- [ ] Update `DependencyInjection/ServiceCollectionExtensions.cs`
  - [ ] Add `AddFunctionalMsSqlWithCircuitBreaker()`
  - [ ] Wire up integration

**Tests:**
- [ ] Closed state allows operations
- [ ] Open state rejects operations
- [ ] Half-open state allows test operation
- [ ] Transitions between states
- [ ] Failure threshold behavior
- [ ] Recovery behavior

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] All tests pass
- [ ] Thread-safety verified
- [ ] Code review approved

---

## 🧪 Phase 4: Testing & Quality Assurance

### 4.1 Unit Test Modernization
**Branch:** `feature/modern-xunit-tests`

- [ ] Update test naming to modern convention
  - [ ] `MethodName_Scenario_ExpectedBehavior`
  - [ ] Use PascalCase consistently

- [ ] Implement parametrized tests
  - [ ] Convert multiple similar tests to `[Theory]`
  - [ ] Use `[InlineData(...)]`

- [ ] Add test helpers in new `Helpers/` directory
  - [ ] `DbContextHelpers.cs`
  - [ ] `TestDataBuilders.cs`
  - [ ] `AsyncTestHelpers.cs`

- [ ] Update assertions
  - [ ] Use `Assert.True/False` correctly
  - [ ] Use `Assert.Throws/ThrowsAsync` for exceptions
  - [ ] Add meaningful assertion messages

### 4.2 Integration Tests
- [ ] Add integration test project if not exists
- [ ] Test against real SQL Server (local/container)
- [ ] Test transaction scenarios
- [ ] Test pagination edge cases
- [ ] Test batch operations at scale
- [ ] Test async streaming with large datasets

### 4.3 Performance Benchmarks
- [ ] Create benchmark project (BenchmarkDotNet)
- [ ] Benchmark batch operations
- [ ] Benchmark async streaming
- [ ] Benchmark query operations
- [ ] Compare before/after modernization
- [ ] Document results

### 4.4 Coverage Analysis
- [ ] Run `dotnet-coverage collect`
- [ ] Target ≥95% code coverage
- [ ] Identify coverage gaps
- [ ] Add missing tests
- [ ] Document excluded code (e.g., diagnostics)

**Verification:**
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` all pass
- [ ] `dotnet-coverage` ≥95%
- [ ] Benchmarks show no regression
- [ ] Code review approved

---

## 📖 Phase 5: Documentation & Release

### 5.1 Code Documentation
- [ ] Update all public API XML docs
- [ ] Add examples to verbose documentation
- [ ] Document new builder patterns
- [ ] Document pagination usage
- [ ] Document batch operations

### 5.2 Migration Guide
- [ ] Create `MIGRATION_v1_to_v2.md`
- [ ] List all new features (opt-in)
- [ ] Show before/after examples
- [ ] Highlight breaking changes (none)
- [ ] Provide upgrade steps

### 5.3 README Updates
- [ ] Update feature list
- [ ] Add new usage examples
- [ ] Update version references
- [ ] Add links to migration guide

### 5.4 CHANGELOG
- [ ] Document all changes
- [ ] Group by category (features, improvements, fixes)
- [ ] Reference issue numbers
- [ ] Include breaking changes (none)

### 5.5 Release Planning
- [ ] Tag version 2.0.0-preview.1
- [ ] Publish to NuGet (pre-release)
- [ ] Gather community feedback
- [ ] Tag version 2.0.0
- [ ] Publish final release

---

## ✅ Quality Gates

### Build Gate
- [ ] `dotnet build` succeeds with zero warnings
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet analyzers` pass (Roslyn, StyleCop, etc.)

### Test Gate
- [ ] `dotnet test` all tests pass
- [ ] Code coverage ≥95%
- [ ] Integration tests pass
- [ ] Performance benchmarks pass

### Review Gate
- [ ] Code review approved by 2+ maintainers
- [ ] No outstanding concerns
- [ ] Documentation reviewed
- [ ] Examples tested

### Compatibility Gate
- [ ] ✅ Zero breaking changes to public APIs
- [ ] ✅ Drop-in replacement verified
- [ ] ✅ Existing code unaffected
- [ ] ✅ Migration guide provided

---

## 🎯 Timeline Estimate

| Phase | Duration | Status |
|-------|----------|--------|
| Phase 1: Audit | 2 days | ⏳ Pending |
| Phase 2: Modernization | 3 days | ⏳ Pending |
| Phase 3a: QueryResults | 2 days | ⏳ Pending |
| Phase 3b: QuerySpecification | 1.5 days | ⏳ Pending |
| Phase 3c: Batch Operations | 1.5 days | ⏳ Pending |
| Phase 3d: Async Streaming | 1 day | ⏳ Pending |
| Phase 4: Testing | 2 days | ⏳ Pending |
| Phase 5: Documentation | 1 day | ⏳ Pending |
| **Total** | **~15 days** | |

*Note: Can be parallelized across team members*

---

## 🔄 CI/CD Integration

### Required Before Merge
```yaml
- Build: dotnet build
- Format: dotnet format --verify-no-changes
- Test: dotnet test --collect:"XPlat Code Coverage"
- Coverage: coverage > 95%
- Analyze: dotnet analyzers
```

### Pre-Release Validation
```bash
# Build package
dotnet pack --configuration Release --output ./nupkg/

# Verify version
dotnet nuget verify ./nupkg/SharpFunctional.MsSql.2.0.0-preview.1.nupkg

# Publish to NuGet
dotnet nuget push ./nupkg/SharpFunctional.MsSql.2.0.0-preview.1.nupkg
```

---

## 📋 Sign-Off

### Phase Approvals
- [ ] Phase 1 Audit - Approved: ________ Date: ________
- [ ] Phase 2 Modernization - Approved: ________ Date: ________
- [ ] Phase 3 Features - Approved: ________ Date: ________
- [ ] Phase 4 Testing - Approved: ________ Date: ________
- [ ] Phase 5 Release - Approved: ________ Date: ________

### Release Sign-Off
- [ ] Backward compatibility verified
- [ ] All tests passing
- [ ] Documentation complete
- [ ] Release ready

**Approved By:** ________________  
**Date:** ________________

---

**Document Version:** 1.0  
**Last Updated:** 2025-01-28  
**Status:** Ready for Phase 1
