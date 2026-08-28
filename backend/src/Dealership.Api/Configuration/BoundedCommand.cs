namespace Dealership.Api.Configuration;

// The twin of BoundedRead for operations whose own deadline result differs: a command keeps its unconfirmed message.
public static class BoundedCommand
{
    private const int DEADLINE_SECONDS = 20;

    public static async Task<IResult> RunAsync(Func<CancellationToken, Task<IResult>> command,
        Func<IResult> deadlineResult, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DEADLINE_SECONDS));

        try
        {
            return await command(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return deadlineResult();
        }
    }
}