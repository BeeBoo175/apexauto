using System.Linq.Expressions;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;

namespace Dealership.Infrastructure.Persistence;

internal static class ActorRows
{
    public static readonly Expression<Func<AccountUser, CurrentActor>> Current =
        user => new CurrentActor(user.Id, user.Role, user.BranchId, user.EmailConfirmed);
}
