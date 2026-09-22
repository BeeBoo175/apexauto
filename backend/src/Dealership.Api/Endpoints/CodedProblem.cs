namespace Dealership.Api.Endpoints;

// The shared shape of the stable 409 answers: a problem document carrying a code the browser recognises, and the
// plain `{ code }` body, with optional blockers, that the account commands answer.
internal static class CodedProblem
{
    public static IResult Conflict(string code, string title)
    {
        return Results.Problem(statusCode: 409, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }

    public static IResult CodeConflict(string code) => Results.Json(new { code }, statusCode: 409);

    // A blocked command always names its blocker list, even when it is empty or missing.
    public static IResult Blocked(object? blockers) =>
        Results.Json(new { code = "blocked", blockers }, statusCode: 409);
}