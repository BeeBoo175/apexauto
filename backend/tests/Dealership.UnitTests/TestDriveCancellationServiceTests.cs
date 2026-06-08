using Dealership.Domain.Common;
using Dealership.Application.Accounts;
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
        var transaction = new RecordingTransaction(new(TestDriveCancellationStatus.Ready, booking.StartsAt.AddTicks(ticks), booking,
            Actor: new(true, AccountRoles.Customer, null)));
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
            booking.StartsAt.AddDays(1), booking, cancellation, Cancelled: true, Actor: new(true, AccountRoles.Customer, null)));
        var service = new TestDriveCancellationService(new CancellationStore(transaction));
        var result = await service.CancelAsync(booking.CustomerId, booking.Id, cancellation.Id, default);

        Assert.Equal(TestDriveCancellationStatus.Succeeded, result.Status);
        Assert.Same(cancellation, result.Cancellation);
        Assert.False(transaction.Committed);
    }

    [Theory]
    [InlineData(0, TestDriveCancellationStatus.Succeeded)]
    [InlineData(1, TestDriveCancellationStatus.Succeeded)]
    [InlineData(900, TestDriveCancellationStatus.TooLate)]
    public async Task StaffCanCancelAfterBookedStartUntilDerivedNoShow(int minutes, TestDriveCancellationStatus expected)
    {
        var booking = Booking();
        var actor = Guid.NewGuid();
        var context = new TestDriveCancellationContext(TestDriveCancellationStatus.Ready,
            booking.StartsAt.AddMinutes(minutes), booking, Actor: new(true, AccountRoles.BranchManager, booking.BranchId),
            BranchActive: true);
        var transaction = new RecordingTransaction(context);
        var result = await new TestDriveCancellationService(new CancellationStore(transaction))
            .CancelByStaffAsync(actor, booking.Id, Guid.NewGuid(), " Customer requested cancellation ", default);

        Assert.Equal(expected, result.Status);
        if (result.Cancellation is { } receipt)
        {
            Assert.Equal("Customer requested cancellation", receipt.Reason);
            Assert.Equal(actor, receipt.StaffId);
            Assert.Equal(TestDriveCancellationKind.StaffCancelled, receipt.Kind);
        }
    }

    [Fact]
    public async Task StaffReceiptReplayPrecedesCurrentOwnershipAndDayCutoff()
    {
        var booking = Booking();
        var actor = Guid.NewGuid();
        var receipt = TestDriveCancellation.ByStaff(Guid.NewGuid(), booking, actor, "Customer cannot attend", booking.StartsAt);
        var context = new TestDriveCancellationContext(TestDriveCancellationStatus.Ready, booking.StartsAt.AddDays(2),
            booking, receipt, true, new(true, AccountRoles.SalesAgent, booking.BranchId), true);
        var transaction = new RecordingTransaction(context);
        var service = new TestDriveCancellationService(new CancellationStore(transaction));
        var replay = await service.CancelByStaffAsync(actor, booking.Id, receipt.Id, " Customer cannot attend ", default);
        Assert.Same(receipt, replay.Cancellation);
        Assert.Equal(TestDriveCancellationStatus.Succeeded, replay.Status);
        Assert.Equal(TestDriveCancellationStatus.Conflict,
            (await service.CancelByStaffAsync(actor, booking.Id, receipt.Id, "Changed reason", default)).Status);
        Assert.False(transaction.Committed);
    }

    private static TestDriveBooking Booking()
    {
        var startsAt = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero);
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new AppointmentSlot(startsAt, startsAt + TestDriveBooking.Duration), startsAt.AddHours(-3));
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
