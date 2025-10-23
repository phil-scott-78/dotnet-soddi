using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace Soddi.Providers.SqlServer;

/// <summary>
/// Retry policy for SQL Server operations
/// </summary>
public static class SqlServerRetryPolicy
{
    public static readonly AsyncRetryPolicy Policy = Polly.Policy.Handle<SqlException>()
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromMilliseconds(500), (_, _, _) => { });
}
