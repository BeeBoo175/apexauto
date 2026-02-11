using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Domain.Common;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentIntakeFixtureAssertions
{
    private static readonly int[] PageSizes = [20, 20, 4];

    public static async Task VerifyAuthenticatedAsync(HttpClient client, string email)
    {
        var customer = DevelopmentFixtureManifest.Customers.SingleOrDefault(item => item.Registration.Email == email);
        var staff = DevelopmentFixtureManifest.Staff.SingleOrDefault(item => item.Email == email);

        if (customer is not null)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/staff/intake/appointments")).StatusCode);

            if (!customer.VerifyEmail)
            {
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/intake/appointments")).StatusCode);
                return;
            }

            var history = await client.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments?page=3");
            Assert.NotNull(history);
            Assert.Equal(44, history.Total);
            Assert.Equal(4, history.Items.Count);
            var ownedIds = DevelopmentIntakeManifest.Appointments.Where(item =>
                item.RegistrationKey == customer.Registration.RegistrationKey).Select(item => item.Id).ToHashSet();
            Assert.All(history.Items, item => Assert.Contains(item.Id, ownedIds));
            var other = DevelopmentIntakeManifest.Appointments.First(item => !ownedIds.Contains(item.Id));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/intake/appointments/{other.Id}")).StatusCode);
            return;
        }

        Assert.NotNull(staff);
        var dates = $"from={DateTimeOffset.UtcNow.AddDays(-7):yyyy-MM-dd}&to={DateTimeOffset.UtcNow.AddDays(31):yyyy-MM-dd}";
        var url = $"/api/staff/intake/appointments?{dates}";

        if (staff.Role is AccountRoles.Administrator or AccountRoles.WorkshopTechnician)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
            return;
        }

        foreach (var status in Enum.GetValues<StaffWorklistStatus>())
        {
            var result = await client.GetFromJsonAsync<StaffWorklistPage>($"{url}&status={status}");
            Assert.NotNull(result);
            var expected = DevelopmentIntakeManifest.Appointments.Where(item =>
                (staff.BranchId == null || item.BranchId == staff.BranchId) &&
                (status == StaffWorklistStatus.All || item.Status == status)).ToArray();
            Assert.Equal(expected.Length, result.Total);
            Assert.Equal(Math.Min(20, expected.Length), result.Items.Count);
            Assert.All(result.Items, item => Assert.Contains(item.Id, expected.Select(row => row.Id)));
        }

        if (staff.BranchId is not null)
        {
            var other = DevelopmentIntakeManifest.Appointments.First(item => item.BranchId != staff.BranchId);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{url}&branchId={other.BranchId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync($"/api/staff/intake/appointments/{other.Id}")).StatusCode);
        }
    }

    public static async Task VerifyAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var appointments = await database.IntakeAppointments.AsNoTracking().ToArrayAsync();
        Assert.Equal(132, appointments.Length);
        Assert.Equal(132, await database.SellerValuations.CountAsync());
        Assert.Equal(528, await database.IntakePhotos.CountAsync());
        Assert.Equal(528, await database.AppointmentPhotos.CountAsync());
        Assert.Equal(132, await database.AppointmentConfirmations.CountAsync());
        Assert.Equal(3, await database.Vehicles.CountAsync());
        Assert.Equal(3, await database.AppointmentCheckIns.CountAsync());
        Assert.Equal(3, await database.AppointmentCheckInMails.CountAsync());
        Assert.Equal(3, await database.AppointmentCancellations.CountAsync());
        Assert.Equal(3, await database.AppointmentCancellationMails.CountAsync());
        Assert.Equal(528, await database.IntakePhotos.CountAsync(photo => photo.AvailableAt != null));
        var schedule = services.GetRequiredService<InspectionSchedule>();
        var malaysia = TimeZoneInfo.FindSystemTimeZoneById(DealershipLocale.MalaysiaTimeZoneId);
        var valuations = await database.SellerValuations.AsNoTracking().ToDictionaryAsync(item => item.AppointmentId);
        var confirmations = await database.EmailVerificationChallenges.Where(item => item.ConsumedAt != null)
            .ToDictionaryAsync(item => item.UserId, item => item.ConsumedAt!.Value);
        foreach (var appointment in appointments)
        {
            Assert.True(confirmations[appointment.OwnerId] <= appointment.CreatedAt);
            Assert.Equal(new InspectionSlot(appointment.StartsAt, appointment.EndsAt),
                schedule.FindSlot(appointment.StartsAt, appointment.CreatedAt));
            Assert.Equal(TimeZoneInfo.ConvertTime(appointment.CreatedAt, malaysia).Year,
                valuations[appointment.Id].ValuationYear);
        }

        await AssertStatesAsync(database);
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

            var bookedIds = DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.Booked)
                .Select(item => item.Id).ToHashSet();
            var expected = appointments.Where(item => bookedIds.Contains(item.Id) &&
                    (staff.BranchId == null || item.BranchId == staff.BranchId))
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
                Assert.Equal(44, result.Total);
                Assert.Equal(PageSizes[page - 1], result.Items.Count);
                actual.AddRange(result.Items.Select(item => item.Id));
            }

            Assert.Equal(expected, actual);
            Assert.Empty((await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, 4, 20), CancellationToken.None))!.Items);
        }

        var first = appointments.Single(item => item.Id == DevelopmentIntakeManifest.Appointments[0].Id);
        await AssertMediaAsync(services, first);
    }

    private static async Task AssertStatesAsync(DealershipDbContext database)
    {
        foreach (var fixture in DevelopmentIntakeManifest.Appointments.Where(item => item.CheckIn is not null))
        {
            var checkIn = await database.AppointmentCheckIns.SingleAsync(item => item.AppointmentId == fixture.Id);
            var appointment = await database.IntakeAppointments.SingleAsync(item => item.Id == fixture.Id);
            Assert.Equal(appointment.StartsAt, checkIn.CheckedInAt);
            Assert.True(checkIn.CheckedInAt < DateTimeOffset.UtcNow);
            Assert.True(checkIn.Matches(fixture.CheckIn!));
            Assert.Equal(DevelopmentFixtureManifest.Staff.Single(item =>
                item.BranchId == fixture.BranchId && item.Role == AccountRoles.SalesAgent).Id, checkIn.StaffId);
            var vehicle = await database.Vehicles.SingleAsync(item => item.Id == checkIn.VehicleId);
            Assert.Equal(VehicleStatus.PendingInspection, vehicle.Status);
            Assert.Equal(fixture.Id, vehicle.IntakeAppointmentId);
            Assert.Equal(fixture.BranchId, vehicle.BranchId);
            Assert.Equal(fixture.CheckIn!.MileageKilometres, vehicle.MileageKilometres);
            Assert.Equal(fixture.Vehicle.MileageKilometres,
                (await database.SellerValuations.SingleAsync(item => item.AppointmentId == fixture.Id)).MileageKilometres);
        }

        foreach (var fixture in DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.Cancelled))
        {
            var cancellation = await database.AppointmentCancellations.SingleAsync(item => item.AppointmentId == fixture.Id);
            var appointment = await database.IntakeAppointments.SingleAsync(item => item.Id == fixture.Id);
            Assert.True(appointment.CreatedAt <= cancellation.CancelledAt);
            Assert.True(cancellation.CancelledAt < appointment.StartsAt);
            Assert.True(appointment.EndsAt < DateTimeOffset.UtcNow);
        }
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
            CancellationMails = await database.AppointmentCancellationMails.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            CheckIns = await database.AppointmentCheckIns.AsNoTracking().OrderBy(item => item.AppointmentId).ToArrayAsync(),
            CheckInMails = await database.AppointmentCheckInMails.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Vehicles = await database.Vehicles.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()
        });
    }
}
