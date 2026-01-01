using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Intake;

public sealed class IntakeBookingStore(DealershipDbContext database) : IIntakeBookingStore
{
    public async Task<IIntakeBookingTransaction> LockAsync(Guid ownerId, BookingRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({"appointment:" + request.Id}, 0))", cancellationToken);
            var user = await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser: true)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);

            if (user is null)
            {
                return new IntakeBookingTransaction(database, transaction, new BookingContext(BookingStatus.Forbidden), string.Empty);
            }

            var existingOwner = await database.IntakeAppointments.Where(item => item.Id == request.Id)
                .Select(item => (Guid?)item.OwnerId).SingleOrDefaultAsync(cancellationToken);

            if (existingOwner is not null)
            {
                var receipt = await AppointmentReceiptReader.ReadAsync(database, request.Id, ownerId, cancellationToken);
                var status = existingOwner == ownerId ? BookingStatus.Ready : BookingStatus.Conflict;
                return new IntakeBookingTransaction(database, transaction, new BookingContext(status, receipt), user.Email!);
            }

            var branchAvailable = await database.Branches.FromSqlInterpolated(
                $"SELECT * FROM \"Branches\" WHERE \"Id\" = {request.BranchId} FOR UPDATE")
                .AnyAsync(branch => branch.IsActive, cancellationToken);
            var ids = request.PhotoIds!.ToArray();
            var photos = await database.IntakePhotos.FromSqlInterpolated(
                $"SELECT * FROM \"IntakePhotos\" WHERE \"Id\" = ANY ({ids}) ORDER BY \"Id\" FOR UPDATE")
                .AsNoTracking().ToArrayAsync(cancellationToken);
            var used = await database.AppointmentPhotos.Where(item => ids.Contains(item.PhotoId))
                .Select(item => item.PhotoId).ToArrayAsync(cancellationToken);
            var candidates = photos.Select(photo => new BookingPhotoCandidate(photo.Id, photo.OwnerId, photo.Angle,
                photo.AvailableAt, photo.ExpiresAt, used.Contains(photo.Id))).ToArray();
            var context = new BookingContext(BookingStatus.Ready, BranchAvailable: branchAvailable, Photos: candidates);
            return new IntakeBookingTransaction(database, transaction, context, user.Email!);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<BookingResult> FindAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        if (!await VerifiedCustomerQuery.ForOwner(database, ownerId, lockUser: false).AnyAsync(cancellationToken))
        {
            return new BookingResult(BookingStatus.Forbidden);
        }

        var receipt = await AppointmentReceiptReader.ReadAsync(database, id, ownerId, cancellationToken);
        return receipt is null ? new BookingResult(BookingStatus.NotFound) : new BookingResult(BookingStatus.Succeeded, receipt);
    }

    public async Task<IReadOnlyList<InspectionSlot>> GetOccupiedSlotsAsync(Guid branchId, DateTimeOffset first,
        DateTimeOffset last, CancellationToken cancellationToken)
    {
        return await database.IntakeAppointments.Where(item => item.BranchId == branchId &&
            item.EndsAt > first && item.StartsAt < last)
            .Select(item => new InspectionSlot(item.StartsAt, item.EndsAt)).ToArrayAsync(cancellationToken);
    }
}
