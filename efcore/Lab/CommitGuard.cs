using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCoreLessons.Lab;

// A commit is permanent: it would write to AdventureWorks for good. Refuse every commit the test's
// transaction could receive through EF (Database.CommitTransaction); raw COMMIT text is
// refused by TransactionGuard.
public sealed class CommitGuard : DbTransactionInterceptor
{
    static InvalidOperationException Refusal() => new(
        "Lab tests never commit: the test's transaction is rolled back when the test ends, so AdventureWorks " +
        "stays unchanged. Remove the commit (SaveChanges already saves inside the test's transaction).");

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
        => throw Refusal();

    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
        => throw Refusal();
}
