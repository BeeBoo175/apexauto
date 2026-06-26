using Dealership.Infrastructure.Photos;
using Dealership.Domain.Photos;
using Dealership.Application.Photos;
using Dealership.Infrastructure.Intake;
using Dealership.Application.Vehicles;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Vehicles;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal sealed class DevelopmentFixtureInterruption
{
    public bool Enabled { get; set; } = true;
    public bool FailInspectionUploadOnce { get; set; }
    public bool FailListingUploadOnce { get; set; }
    public bool FailReservationCommitOnce { get; set; }
    private int _uploads;

    public void Configure(IServiceCollection services)
    {
        services.AddSingleton<IPhotoStorage>(provider =>
            new PhotoStorage(ActivatorUtilities.CreateInstance<S3PhotoStorage>(provider), this));
        services.AddScoped<IReservationStore>(provider =>
            new ReservationStoreProbe(ActivatorUtilities.CreateInstance<ReservationStore>(provider), this));
    }

    private sealed class ReservationStoreProbe(IReservationStore inner, DevelopmentFixtureInterruption fault) : IReservationStore
    {
        public async Task<IReservationTransaction> LockAsync(Guid customerId, Guid vehicleId, ReservationRequest request,
            CancellationToken cancellationToken)
        {
            return new TransactionProbe(await inner.LockAsync(customerId, vehicleId, request, cancellationToken), fault);
        }
    }

    private sealed class TransactionProbe(IReservationTransaction inner, DevelopmentFixtureInterruption fault) : IReservationTransaction
    {
        public ReservationContext Context => inner.Context;

        public async Task<ReservationStatus> CommitAsync(Reservation? reservation, CancellationToken cancellationToken)
        {
            var result = await inner.CommitAsync(reservation, cancellationToken);
            if (reservation is not null && result == ReservationStatus.Succeeded && fault.FailReservationCommitOnce)
            {
                fault.FailReservationCommitOnce = false;
                throw new IOException("Injected lost acknowledgement after the fixture hold committed.");
            }

            return result;
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class PhotoStorage(IPhotoStorage inner, DevelopmentFixtureInterruption fault) : IPhotoStorage
    {
        public async Task PutAsync(IPhotoObject photo, Stream content, CancellationToken cancellationToken)
        {
            await inner.PutAsync(photo, content, cancellationToken);
            if (fault.FailInspectionUploadOnce && photo.ObjectKey.StartsWith("inspection/photos/", StringComparison.Ordinal))
            {
                fault.FailInspectionUploadOnce = false;
                throw new IOException("Injected lost acknowledgement for the inspection fixture upload.");
            }

            if (fault.FailListingUploadOnce && photo.ObjectKey.StartsWith("listings/photos/", StringComparison.Ordinal))
            {
                fault.FailListingUploadOnce = false;
                throw new IOException("Injected lost acknowledgement for the listing fixture upload.");
            }

            // The fifth upload is meant to be the first appointment bundle; account pictures are seeded before it.
            if (fault.Enabled && !photo.ObjectKey.StartsWith("accounts/", StringComparison.Ordinal) &&
                Interlocked.Increment(ref fault._uploads) == 5)
            {
                throw new IOException("Injected lost acknowledgement after the first appointment bundle was inserted.");
            }
        }

        public Task CopyToAsync(IPhotoObject photo, Stream destination, CancellationToken cancellationToken)
        {
            return inner.CopyToAsync(photo, destination, cancellationToken);
        }

        public Task DeleteAsync(IPhotoObject photo, CancellationToken cancellationToken)
        {
            return inner.DeleteAsync(photo, cancellationToken);
        }
    }
}
