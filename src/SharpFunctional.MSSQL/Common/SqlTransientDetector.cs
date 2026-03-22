using Microsoft.Data.SqlClient;

namespace SharpFunctional.MsSql.Common;

internal static class SqlTransientDetector
{
    private static readonly HashSet<int> TransientSqlErrorNumbers =
    [
        -2,     // Timeout expired
        4060,   // Cannot open database
        10928,  // Resource limit reached
        10929,  // Resource limit reached
        40197,  // Service encountered an error
        40501,  // Service is busy
        40613,  // Database unavailable
        49918,  // Cannot process request
        49919,  // Too many create/update operations
        49920,  // Too many operations in progress
        1205    // Deadlock victim
    ];

    internal static bool IsTransient(Exception exception)
    {
        if (exception is SqlException sqlException)
        {
            return sqlException.Errors.Cast<SqlError>().Any(error => TransientSqlErrorNumbers.Contains(error.Number));
        }

        return exception is TimeoutException;
    }
}
