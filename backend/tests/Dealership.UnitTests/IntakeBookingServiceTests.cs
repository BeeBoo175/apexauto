using Dealership.Application.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Intake;

namespace Dealership.UnitTests;

public sealed class IntakeBookingServiceTests
{
    [Theory]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("currency")]
    public async Task ChangedRangeOrCurrencyReturnsCanonicalQuoteWithoutCommitting(string changed)
    {
        var clock = new BookingClock();
        var valuations = new ValuationService(clock);
        var make = VehicleTaxonomy.Makes[0];
        var model = make.Models[0];
        var vehicle = new ValuationRequest(make.Name, model.Name, model.Trims[0].Name, 2020, 90000);
        var quote = valuations.Calculate(vehicle).Quote!;
        var accepted = changed switch
        {
            "minimum" => quote with { MinValuation = quote.MinValuation + 1 },
            "maximum" => quote with { MaxValuation = quote.MaxValuation + 1 },
            "currency" => quote with { Currency = "USD" },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        var owner = Guid.NewGuid();
        var candidates = Enum.GetValues<PhotoAngle>().Select(angle => new BookingPhotoCandidate(Guid.NewGuid(), owner,
            angle, clock.GetUtcNow().AddMinutes(-1), clock.GetUtcNow().AddHours(24), false, false)).ToArray();
        var transaction = new RecordingTransaction(candidates);
        var service = new IntakeBookingService(new BookingStore(transaction), valuations,
            new InspectionSchedule(new TimeOnly(9, 0), new TimeOnly(18, 0), 90, 30, 120, [DayOfWeek.Saturday]), clock);
        var request = new BookingRequest(Guid.NewGuid(), Guid.NewGuid(), clock.GetUtcNow().AddDays(1).AddHours(1),
            "WYY 8821", vehicle, accepted, candidates.Select(photo => photo.Id).ToArray());
        var result = await service.BookAsync(owner, request, CancellationToken.None);
        Assert.Equal(BookingStatus.QuoteChanged, result.Status);
        Assert.Equal(quote, result.FreshQuote);
        Assert.False(transaction.Committed);
    }

    private sealed class BookingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class BookingStore(RecordingTransaction transaction) : IIntakeBookingStore
    {
        public Task<IIntakeBookingTransaction> LockAsync(Guid ownerId, BookingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<IIntakeBookingTransaction>(transaction);
        }

        public Task<BookingResult> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<InspectionSlot>> GetOccupiedSlotsAsync(Guid branchId, DateTimeOffset first,
            DateTimeOffset last, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingTransaction(IReadOnlyList<BookingPhotoCandidate> photos) : IIntakeBookingTransaction
    {
        public BookingContext Context { get; } = new(BookingStatus.Ready, BranchAvailable: true, Photos: photos);
        public bool Committed { get; private set; }

        public Task<BookingResult> CommitAsync(IntakeAppointment appointment,
            IReadOnlyList<AppointmentPhoto> attachments, CancellationToken cancellationToken)
        {
            Committed = true;
            return Task.FromResult(new BookingResult(BookingStatus.Succeeded));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
