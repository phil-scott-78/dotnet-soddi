using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;

namespace Soddi.Tasks.SqlServer;

public static class RetryPolicy
{
    public static readonly AsyncRetryPolicy Policy = Polly.Policy.Handle<SqlException>()
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromMilliseconds(500), (ex, _, _) => { });
}
