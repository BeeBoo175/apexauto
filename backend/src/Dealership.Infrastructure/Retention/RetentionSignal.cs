using System.Threading.Channels;

namespace Dealership.Infrastructure.Retention;

public sealed class RetentionSignal
{
    // Retention deadlines are persisted; duplicate execution hints can be coalesced.
    private readonly Channel<bool> _requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    public void Request()
    {
        _requests.Writer.TryWrite(true);
    }

    public ValueTask<bool> WaitAsync(CancellationToken cancellationToken)
    {
        return _requests.Reader.ReadAsync(cancellationToken);
    }
}
