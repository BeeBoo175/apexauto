using System.Security.Cryptography;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentIntakeFixtureAssertions
{
    private static readonly int[] PageSizes = [20, 20, 1];

    public static async Task AssertInterruptedMediaAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        Assert.Null((await database.IntakePhotos.SingleAsync()).AvailableAt);
        Assert.Equal(0, await database.IntakeAppointments.CountAsync());
    }

    public static async Task VerifyAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var appointments = await database.IntakeAppointments.AsNoTracking().ToArrayAsync();
        Assert.Equal(123, appointments.Length);
        Assert.Equal(123, await database.SellerValuations.CountAsync());
        Assert.Equal(492, await database.IntakePhotos.CountAsync());
        Assert.Equal(492, await database.AppointmentPhotos.CountAsync());
        Assert.Equal(123, await database.AppointmentConfirmations.CountAsync());
        Assert.Equal(0, await database.Vehicles.CountAsync());
        Assert.Equal(492, await database.IntakePhotos.CountAsync(photo => photo.AvailableAt != null));
        var query = new StaffWorklistQuery(appointments.Min(item => item.StartsAt).AddDays(-1),
            appointments.Max(item => item.EndsAt).AddDays(1), StaffWorklistStatus.Booked, null, 1, 20);

        foreach (var staff in DevelopmentFixtureManifest.Staff)
        {
            var store = services.GetRequiredService<IStaffWorklistStore>();

            if (staff.Role is AccountRoles.Administrator or AccountRoles.WorkshopTechnician)
            {
                Assert.Null(await store.ListAsync(staff.Id, query, CancellationToken.None));
                continue;
            }

            var expected = appointments.Where(item => staff.BranchId == null || item.BranchId == staff.BranchId)
                .OrderBy(item => item.StartsAt).ThenBy(item => item.Id).Select(item => item.Id).ToArray();
            var actual = new List<Guid>();

            for (var page = 1; actual.Count < expected.Length; page++)
            {
                var result = await store.ListAsync(staff.Id, query with { Page = page }, CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal(expected.Length, result.Total);
                Assert.Equal(Math.Min(20, expected.Length - actual.Count), result.Items.Count);
                actual.AddRange(result.Items.Select(item => item.Id));
            }

            Assert.Equal(expected, actual);
            Assert.Empty((await store.ListAsync(staff.Id, query with { Page = int.MaxValue }, CancellationToken.None))!.Items);

            if (staff.BranchId is not null)
            {
                var otherBranch = DevelopmentFixtureManifest.Branches.First(branch => branch.Id != staff.BranchId).Id;
                Assert.Null(await store.ListAsync(staff.Id, query with { BranchId = otherBranch }, CancellationToken.None));
            }
        }

        foreach (var customer in DevelopmentFixtureManifest.Customers)
        {
            var owner = await database.CustomerProfiles.SingleAsync(profile =>
                profile.RegistrationKey == customer.Registration.RegistrationKey);
            var history = services.GetRequiredService<IAppointmentHistoryStore>();

            if (!customer.VerifyEmail)
            {
                Assert.Null(await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, 1, 20), CancellationToken.None));
                continue;
            }

            var actual = new List<Guid>();
            var expected = appointments.Where(item => item.OwnerId == owner.UserId)
                .OrderByDescending(item => item.StartsAt).ThenByDescending(item => item.Id).Select(item => item.Id);

            for (var page = 1; page <= 3; page++)
            {
                var result = await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, page, 20), CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal(41, result.Total);
                Assert.Equal(PageSizes[page - 1], result.Items.Count);
                actual.AddRange(result.Items.Select(item => item.Id));
            }

            Assert.Equal(expected, actual);
            Assert.Empty((await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, 4, 20), CancellationToken.None))!.Items);
        }

        var first = appointments.Single(item => item.Id == DevelopmentIntakeManifest.Appointments[0].Id);
        await AssertMediaAsync(services, first);
        var last = appointments.Single(item => item.Id == DevelopmentIntakeManifest.Appointments[^1].Id);
        Assert.Equal(CancellationStatus.Succeeded, (await services.GetRequiredService<IAppointmentCancellationStore>()
            .CancelAsync(last.OwnerId, last.Id, CancellationToken.None)).Status);
    }

    private static async Task AssertMediaAsync(IServiceProvider services, IntakeAppointment appointment)
    {
        var photos = services.GetRequiredService<IntakePhotoService>();
        var database = services.GetRequiredService<DealershipDbContext>();
        var otherOwner = await database.CustomerProfiles.Where(profile => profile.UserId != appointment.OwnerId &&
            profile.RegistrationKey == DevelopmentFixtureManifest.Customers[2].Registration.RegistrationKey)
            .Select(profile => profile.UserId).SingleAsync();

        foreach (var angle in Enum.GetValues<PhotoAngle>())
        {
            var id = DevelopmentIntakeManifest.PhotoId(appointment.Id, angle);
            using var expected = DevelopmentFixturePhotos.Open(angle);
            using var actual = new MemoryStream();
            Assert.Equal(PhotoStatus.Available, (await photos.DownloadAsync(appointment.OwnerId, id, actual, CancellationToken.None)).Status);
            Assert.Equal(await SHA256.HashDataAsync(expected), SHA256.HashData(actual.ToArray()));
            Assert.Equal(PhotoStatus.NotFound, (await photos.GetReceiptAsync(otherOwner, id, CancellationToken.None)).Status);
        }

        Assert.Equal(BookingStatus.NotFound, (await services.GetRequiredService<IntakeBookingService>()
            .GetReceiptAsync(otherOwner, appointment.Id, CancellationToken.None)).Status);
    }

    public static async Task<string> SnapshotAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        return JsonSerializer.Serialize(new
        {
            Appointments = await database.IntakeAppointments.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Valuations = await database.SellerValuations.AsNoTracking().OrderBy(item => item.AppointmentId).ToArrayAsync(),
            Photos = await database.IntakePhotos.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Attachments = await database.AppointmentPhotos.AsNoTracking().OrderBy(item => item.AppointmentId).ThenBy(item => item.PhotoId).ToArrayAsync(),
            Confirmations = await database.AppointmentConfirmations.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Cancellations = await database.AppointmentCancellations.AsNoTracking().OrderBy(item => item.AppointmentId).ToArrayAsync(),
            CancellationMails = await database.AppointmentCancellationMails.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
        });
    }
}
