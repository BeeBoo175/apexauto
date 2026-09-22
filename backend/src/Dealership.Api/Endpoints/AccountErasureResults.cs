using Dealership.Application.Accounts;

namespace Dealership.Api.Endpoints;

// One answer table for customer and staff erasure; only the blocker list a Blocked result carries differs.
internal static class AccountErasureResults
{
    public static IResult Map(AccountErasureResult result, object? blockers)
    {
        return result.Status switch
        {
            AccountErasureStatus.Erased => Results.Ok(result.Receipt),
            AccountErasureStatus.Forbidden => Results.Forbid(),
            AccountErasureStatus.NotFound => Results.NotFound(),
            AccountErasureStatus.WrongPassword => CodedProblem.CodeConflict("wrong-password"),
            AccountErasureStatus.Refused => CodedProblem.CodeConflict("protected"),
            AccountErasureStatus.Conflict => CodedProblem.CodeConflict("conflict"),
            AccountErasureStatus.SessionChanged => CodedProblem.CodeConflict("session-changed"),
            AccountErasureStatus.RetryLater => CodedProblem.CodeConflict("retry-later"),
            AccountErasureStatus.Blocked => CodedProblem.Blocked(blockers),
            _ => Results.Problem(statusCode: 400,
                title: "Provide an operation id, a reason of at most 1000 characters and your password.")
        };
    }
}