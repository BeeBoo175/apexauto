using Dealership.Application.TestDrives;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;

namespace Dealership.UnitTests;

public sealed class TestDriveCancellationServiceTests
{
    [Theory]
    [InlineData(-1, TestDriveCancellationStatus.Succeeded)]
    [InlineData(0, TestDriveCancellationStatus.TooLate)]
    [InlineData(1, TestDriveCancellationStatus.TooLate)]
    public async Task CustomerCutoffExcludesExactBookedStart(int ticks, TestDriveCancellationStatus expected)
    {
        var booking = Booking();
        var transaction = new RecordingTransaction(new(TestDriveCancellationStatus.Ready, booking.StartsAt.AddTicks(ticks), booking));
        var service = new TestDriveCancellationService(new CancellationStore(transaction));
        var result = await service.CancelAsync(booking.CustomerId, booking.Id, Guid.NewGuid(), default);

        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == TestDriveCancellationStatus.Succeeded, transaction.Committed);
    }

    [Fact]
    public async Task ExactCommittedReceiptRemainsRecoverableAfterCutoff()
    {
        var booking = Booking();
        var cancellation = new TestDriveCancellation(Guid.NewGuid(), booking, booking.CustomerId, booking.StartsAt.AddMinutes(-1));
        var transaction = new RecordingTransaction(new(TestDriveCancellationStatus.Ready,
            booking.StartsAt.AddDays(1), booking, cancellation, Cancelled: true));
        var service = new TestDriveCancellationService(new CancellationStore(transaction));
        var result = await service.CancelAsync(booking.CustomerId, booking.Id, cancellation.Id, default);

        Assert.Equal(TestDriveCancellationStatus.Succeeded, result.Status);
        Assert.Same(cancellation, result.Cancellation);
        Assert.False(transaction.Committed);
    }

    private static TestDriveBooking Booking()
    {
        var startsAt = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new InspectionSlot(startsAt, startsAt + TestDriveBooking.Duration), startsAt.AddHours(-3));
    }

    private sealed class CancellationStore(RecordingTransaction transaction) : ITestDriveCancellationStore
    {
        public Task<ITestDriveCancellationTransaction> LockAsync(Guid customerId, Guid bookingId, Guid operationId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ITestDriveCancellationTransaction>(transaction);
        }
    }

    private sealed class RecordingTransaction(TestDriveCancellationContext context) : ITestDriveCancellationTransaction
    {
        public TestDriveCancellationContext Context => context;
        public bool Committed { get; private set; }

        public Task<TestDriveCancellationResult> CommitAsync(TestDriveCancellation cancellation, CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.FromResult(new TestDriveCancellationResult(TestDriveCancellationStatus.Succeeded, cancellation));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
