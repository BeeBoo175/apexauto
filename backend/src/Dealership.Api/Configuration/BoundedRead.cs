namespace Dealership.Api.Configuration;

public static class BoundedRead
{
    private const int DEADLINE_SECONDS = 20;

    public static async Task<IResult> RunAsync(Func<CancellationToken, Task<IResult>> read,
        string unavailableTitle, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DEADLINE_SECONDS));

        try
        {
            return await read(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 503, title: unavailableTitle);
        }
    }
}
