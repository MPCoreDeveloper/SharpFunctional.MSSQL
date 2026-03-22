using System.Linq.Expressions;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.EntityFrameworkCore;
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
}
