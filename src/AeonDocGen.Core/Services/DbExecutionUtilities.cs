using System.Data;
using System.Data.Common;

namespace AeonDocGen.Core.Services;

internal static class DbExecutionUtilities
{
    public static async Task OpenConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
            return;
        }

        connection.Open();
    }

    public static async Task CommitTransactionAsync(IDbTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is DbTransaction dbTransaction)
        {
            await dbTransaction.CommitAsync(cancellationToken);
            return;
        }

        transaction.Commit();
    }

    public static async Task RollbackTransactionAsync(IDbTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction is DbTransaction dbTransaction)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            return;
        }

        transaction.Rollback();
    }
}
