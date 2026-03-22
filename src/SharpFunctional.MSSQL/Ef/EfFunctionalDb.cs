using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
using SharpFunctional.MsSql.Common;
using static LanguageExt.Prelude;

namespace SharpFunctional.MsSql.Ef;

/// <summary>
/// Provides functional Entity Framework Core access with explicit tracking behavior.
/// </summary>
/// <remarks>
/// Default queries are no-tracking. Use <see cref="WithTracking"/> to enable tracking explicitly.
/// </remarks>
public sealed class EfFunctionalDb(DbContext? dbContext, bool trackingEnabled = false)
{
    private DbContext? Context => dbContext;
    private bool TrackingEnabled => trackingEnabled;

    /// <summary>
    /// Creates a copy of this accessor with tracking enabled.
    /// </summary>
    public EfFunctionalDb WithTracking() => new(Context, trackingEnabled: true);

    /// <summary>
    /// Gets an entity by strongly typed primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="id">The primary key value to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Option<T>> GetByIdAsync<T, TId>(TId id, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return Option<T>.None;
        }

        try
        {
            var predicate = BuildPrimaryKeyPredicate<T, TId>(Context, id);
            return await predicate.Match(
                    Some: async p =>
                    {
                        var query = SetForQuery<T>(Context, TrackingEnabled);
                        var entity = await query.FirstOrDefaultAsync(p, cancellationToken).ConfigureAwait(false);
                        return Optional(entity);
                    },
                    None: () => Task.FromResult(Option<T>.None))
                .ConfigureAwait(false);
        }
        catch
        {
            return Option<T>.None;
        }
    }

    /// <summary>
    /// Finds a single entity matching a predicate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Option<T>> FindOneAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null || predicate is null)
        {
            return Option<T>.None;
        }

        try
        {
            var query = SetForQuery<T>(Context, TrackingEnabled);
            var entity = await query.FirstOrDefaultAsync(predicate, cancellationToken).ConfigureAwait(false);
            return Optional(entity);
        }
        catch
        {
            return Option<T>.None;
        }
    }

    /// <summary>
    /// Queries entities matching a predicate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Seq<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null || predicate is null)
        {
            return Seq<T>();
        }

        try
        {
            var query = SetForQuery<T>(Context, TrackingEnabled);
            var entities = await query.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            return toSeq(entities);
        }
        catch
        {
            return Seq<T>();
        }
    }

    /// <summary>
    /// Adds an entity to the current context.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task<Fin<Unit>> AddAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<Unit>(Error.New("EF backend is not configured."));
        }

        if (entity is null)
        {
            return FinFail<Unit>(Error.New("Entity cannot be null."));
        }

        try
        {
            await Context.Set<T>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
            return unit;
        }
        catch (Exception exception)
        {
            return FinFail<Unit>(Error.New(exception));
        }
    }

    /// <summary>
    /// Saves changes for an entity, attaching detached instances as modified when needed.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task<Fin<Unit>> SaveAsync<T>(T entity, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<Unit>(Error.New("EF backend is not configured."));
        }

        if (entity is null)
        {
            return FinFail<Unit>(Error.New("Entity cannot be null."));
        }

        try
        {
            var entry = Context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                Context.Set<T>().Update(entity);
            }

            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return unit;
        }
        catch (Exception exception)
        {
            return FinFail<Unit>(Error.New(exception));
        }
    }

    /// <summary>
    /// Deletes an entity by strongly typed primary key.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TId">The primary key type.</typeparam>
    /// <param name="id">The primary key value of the entity to delete.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task<Fin<Unit>> DeleteByIdAsync<T, TId>(TId id, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<Unit>(Error.New("EF backend is not configured."));
        }

        try
        {
            var predicate = BuildPrimaryKeyPredicate<T, TId>(Context, id);
            if (predicate.IsNone)
            {
                return FinFail<Unit>(Error.New("Entity primary key metadata is missing or unsupported."));
            }

            var entity = await predicate.Match(
                    Some: p => Context.Set<T>().FirstOrDefaultAsync(p, cancellationToken),
                    None: () => Task.FromResult<T?>(null))
                .ConfigureAwait(false);

            if (entity is null)
            {
                return unit;
            }

            Context.Set<T>().Remove(entity);
            await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return unit;
        }
        catch (Exception exception)
        {
            return FinFail<Unit>(Error.New(exception));
        }
    }

    /// <summary>
    /// Counts entities matching a predicate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Fin<int>> CountAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<int>(Error.New("EF backend is not configured."));
        }

        if (predicate is null)
        {
            return FinFail<int>(Error.New("Predicate cannot be null."));
        }

        try
        {
            var query = SetForQuery<T>(Context, TrackingEnabled);
            var count = await query.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
            return count;
        }
        catch (Exception exception)
        {
            return FinFail<int>(Error.New(exception));
        }
    }

    /// <summary>
    /// Checks whether any entity matches a predicate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    public async Task<Fin<bool>> AnyAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<bool>(Error.New("EF backend is not configured."));
        }

        if (predicate is null)
        {
            return FinFail<bool>(Error.New("Predicate cannot be null."));
        }

        try
        {
            var query = SetForQuery<T>(Context, TrackingEnabled);
            var any = await query.AnyAsync(predicate, cancellationToken).ConfigureAwait(false);
            return any;
        }
        catch (Exception exception)
        {
            return FinFail<bool>(Error.New(exception));
        }
    }

    /// <summary>
    /// Queries entities using a reusable <see cref="IQuerySpecification{T}"/> that encapsulates
    /// filter, include, ordering, and paging logic.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="specification">The query specification to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>A read-only list of matching entities, or <see cref="Option{T}.None"/> when the context is not configured.</returns>
    public async Task<Option<IReadOnlyList<T>>> FindAsync<T>(
        IQuerySpecification<T> specification,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null || specification is null)
        {
            return Option<IReadOnlyList<T>>.None;
        }

        using var activity = StartEfActivity("ef.find.spec", typeof(T).Name);

        try
        {
            IQueryable<T> query = SetForQuery<T>(Context, TrackingEnabled);

            if (specification.Predicate is not null)
            {
                query = query.Where(specification.Predicate);
            }

            foreach (var include in specification.Includes)
            {
                query = query.Include(include);
            }

            if (specification.OrderBy is not null)
            {
                query = specification.IsDescending
                    ? query.OrderByDescending(specification.OrderBy)
                    : query.OrderBy(specification.OrderBy);
            }

            if (specification.Skip.HasValue)
            {
                query = query.Skip(specification.Skip.Value);
            }

            if (specification.Take.HasValue)
            {
                query = query.Take(specification.Take.Value);
            }

            var results = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.ItemCountTag, results.Count);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return results.AsReadOnly();
        }
        catch
        {
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error);
            return Option<IReadOnlyList<T>>.None;
        }
    }

    /// <summary>
    /// Queries entities matching a predicate with server-side pagination.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="pageNumber">The 1-based page number. Values less than 1 are clamped to 1.</param>
    /// <param name="pageSize">Items per page (1–1000). Values outside this range are clamped.</param>
    /// <param name="cancellationToken">Token used to cancel the query.</param>
    /// <returns>A paginated result set or a failure when the EF backend is not configured.</returns>
    public async Task<Fin<QueryResults<T>>> FindPaginatedAsync<T>(
        Expression<Func<T, bool>> predicate,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<QueryResults<T>>(Error.New("EF backend is not configured."));
        }

        if (predicate is null)
        {
            return FinFail<QueryResults<T>>(Error.New("Predicate cannot be null."));
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        using var activity = StartEfActivity("ef.find.paginated", typeof(T).Name);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.PageNumberTag, pageNumber);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.PageSizeTag, pageSize);

        try
        {
            var baseQuery = SetForQuery<T>(Context, TrackingEnabled).Where(predicate);

            var totalCount = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

            var items = await baseQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.ItemCountTag, items.Count);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return new QueryResults<T>(items.AsReadOnly(), totalCount, pageNumber, pageSize);
        }
        catch (Exception exception)
        {
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<QueryResults<T>>(Error.New(exception));
        }
    }

    /// <summary>
    /// Streams query results without materializing all entities in memory.
    /// Ideal for processing large data sets.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression to apply.</param>
    /// <param name="cancellationToken">Token used to cancel the stream.</param>
    /// <returns>An async enumerable of matching entities.</returns>
    public async IAsyncEnumerable<T> StreamAsync<T>(
        Expression<Func<T, bool>> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null || predicate is null)
        {
            yield break;
        }

        var query = SetForQuery<T>(Context, TrackingEnabled).Where(predicate);

        await foreach (var entity in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            yield return entity;
        }
    }

    /// <summary>
    /// Inserts multiple entities in configurable batches for improved throughput.
    /// Each batch is saved in a single <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> call.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <param name="batchSize">Maximum entities per save. Values less than 1 are clamped to 1.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The total number of state entries written, or a failure.</returns>
    public async Task<Fin<int>> InsertBatchAsync<T>(
        IEnumerable<T> entities,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<int>(Error.New("EF backend is not configured."));
        }

        if (entities is null)
        {
            return FinFail<int>(Error.New("Entities collection cannot be null."));
        }

        batchSize = Math.Max(1, batchSize);

        using var activity = StartEfActivity("ef.batch.insert", typeof(T).Name);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BatchSizeTag, batchSize);

        try
        {
            var totalInserted = 0;

            foreach (var batch in Chunk(entities, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Context.Set<T>().AddRangeAsync(batch, cancellationToken).ConfigureAwait(false);
                totalInserted += await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.ItemCountTag, totalInserted);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return totalInserted;
        }
        catch (Exception exception)
        {
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<int>(Error.New(exception));
        }
    }

    /// <summary>
    /// Updates multiple already-tracked entities in configurable batches.
    /// Each batch is saved in a single <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> call.
    /// Entities that are not tracked by the context are attached as <see cref="EntityState.Modified"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <param name="batchSize">Maximum entities per save. Values less than 1 are clamped to 1.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The total number of state entries written, or a failure.</returns>
    public async Task<Fin<int>> UpdateBatchAsync<T>(
        IEnumerable<T> entities,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<int>(Error.New("EF backend is not configured."));
        }

        if (entities is null)
        {
            return FinFail<int>(Error.New("Entities collection cannot be null."));
        }

        batchSize = Math.Max(1, batchSize);

        using var activity = StartEfActivity("ef.batch.update", typeof(T).Name);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BatchSizeTag, batchSize);

        try
        {
            var totalUpdated = 0;

            foreach (var batch in Chunk(entities, batchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var entity in batch)
                {
                    var entry = Context.Entry(entity);
                    if (entry.State == EntityState.Detached)
                    {
                        Context.Set<T>().Update(entity);
                    }
                }

                totalUpdated += await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.ItemCountTag, totalUpdated);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return totalUpdated;
        }
        catch (Exception exception)
        {
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<int>(Error.New(exception));
        }
    }

    /// <summary>
    /// Deletes entities matching a predicate in configurable batches.
    /// Each batch is saved in a single <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> call.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">Filter expression identifying entities to delete.</param>
    /// <param name="batchSize">Maximum entities per delete batch. Values less than 1 are clamped to 1.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The total number of entities deleted, or a failure.</returns>
    public async Task<Fin<int>> DeleteBatchAsync<T>(
        Expression<Func<T, bool>> predicate,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (Context is null)
        {
            return FinFail<int>(Error.New("EF backend is not configured."));
        }

        if (predicate is null)
        {
            return FinFail<int>(Error.New("Predicate cannot be null."));
        }

        batchSize = Math.Max(1, batchSize);

        using var activity = StartEfActivity("ef.batch.delete", typeof(T).Name);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BatchSizeTag, batchSize);

        try
        {
            var totalDeleted = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = await Context.Set<T>()
                    .Where(predicate)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (batch.Count == 0)
                {
                    break;
                }

                Context.Set<T>().RemoveRange(batch);
                totalDeleted += await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.ItemCountTag, totalDeleted);
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, true);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return totalDeleted;
        }
        catch (Exception exception)
        {
            activity?.SetTag(SharpFunctionalMsSqlDiagnostics.SuccessTag, false);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            return FinFail<int>(Error.New(exception));
        }
    }

    private static IQueryable<T> SetForQuery<T>(DbContext dbContext, bool trackingEnabled)
        where T : class
    {
        var set = dbContext.Set<T>().AsQueryable();
        return trackingEnabled ? set : set.AsNoTracking();
    }

    private static Option<Expression<Func<T, bool>>> BuildPrimaryKeyPredicate<T, TId>(DbContext dbContext, TId id)
        where T : class
    {
        var entityType = dbContext.Model.FindEntityType(typeof(T));
        var primaryKey = entityType?.FindPrimaryKey();

        if (primaryKey is null || primaryKey.Properties.Count != 1)
        {
            return Option<Expression<Func<T, bool>>>.None;
        }

        var keyProperty = primaryKey.Properties[0];
        var keyType = keyProperty.ClrType;

        if (keyType != typeof(TId))
        {
            return Option<Expression<Func<T, bool>>>.None;
        }

        var entityParameter = Expression.Parameter(typeof(T), "entity");
        var keyAccess = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(TId)],
            entityParameter,
            Expression.Constant(keyProperty.Name));

        var equals = Expression.Equal(keyAccess, Expression.Constant(id, typeof(TId)));
        return Expression.Lambda<Func<T, bool>>(equals, entityParameter);
    }

    private static Activity? StartEfActivity(string operation, string entityType)
    {
        var activity = SharpFunctionalMsSqlDiagnostics.ActivitySource.StartActivity("sharpfunctional.mssql.ef");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.BackendTag, "ef");
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.OperationTag, operation);
        activity?.SetTag(SharpFunctionalMsSqlDiagnostics.EntityTypeTag, entityType);
        activity?.SetTag("db.system", "mssql");
        return activity;
    }

    private static IEnumerable<List<TItem>> Chunk<TItem>(IEnumerable<TItem> source, int size)
    {
        var batch = new List<TItem>(size);

        foreach (var item in source)
        {
            batch.Add(item);

            if (batch.Count >= size)
            {
                yield return batch;
                batch = new List<TItem>(size);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}
