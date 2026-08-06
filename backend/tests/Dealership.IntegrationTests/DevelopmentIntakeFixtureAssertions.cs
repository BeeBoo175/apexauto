using Dealership.Infrastructure.Intake;
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
    private static readonly int[] PageSizes = [.. Enumerable.Range(0,
        (DevelopmentIntakeManifest.TotalPerBranch + 24) / 25)
        .Select(page => Math.Min(25, DevelopmentIntakeManifest.TotalPerBranch - page * 25))];

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

            var history = await client.GetFromJsonAsync<AppointmentHistoryPage>("/api/intake/appointments?page=2");
            Assert.NotNull(history);
            if (!DevelopmentIntakeManifest.Appointments.Any(item =>
                item.RegistrationKey == customer.Registration.RegistrationKey))
            {
                Assert.Equal(0, history.Total);
                Assert.Empty(history.Items);
                return;
            }
            Assert.Equal(DevelopmentIntakeManifest.TotalPerBranch, history.Total);
            Assert.Equal(PageSizes[1], history.Items.Count);
            var ownedIds = DevelopmentIntakeManifest.Appointments.Where(item =>
                item.RegistrationKey == customer.Registration.RegistrationKey).Select(item => item.Id).ToHashSet();
            Assert.All(history.Items, item => Assert.Contains(item.Id, ownedIds));
            var other = DevelopmentIntakeManifest.Appointments.First(item => !ownedIds.Contains(item.Id));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/intake/appointments/{other.Id}")).StatusCode);
            return;
        }

        Assert.NotNull(staff);

        var url = "/api/staff/intake/appointments?";

        if (staff.Role is AccountRoles.Administrator or AccountRoles.WorkshopTechnician)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
            return;
        }

        foreach (var status in Enum.GetValues<StaffWorklistStatus>())
        {
            var token = JsonNamingPolicy.CamelCase.ConvertName(status.ToString());
            var result = await client.GetFromJsonAsync<StaffWorklistPage>($"{url}&view={token}");
            Assert.NotNull(result);
            var expected = DevelopmentIntakeManifest.Appointments.Where(item =>
                (staff.BranchId == null || item.BranchId == staff.BranchId) &&
                (status == StaffWorklistStatus.All || item.Status == status)).ToArray();
            Assert.Equal(expected.Length, result.Total);
            Assert.Equal(Math.Min(25, expected.Length), result.Items.Count);
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
        await database.Database.OpenConnectionAsync();
        var appointments = await database.IntakeAppointments.AsNoTracking().ToArrayAsync();
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count, appointments.Length);
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count * 4, await database.IntakePhotos.CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count * 4, await database.AppointmentPhotos.CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count, await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Confirmation).CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.CheckedInPerBranch * 3, await database.Vehicles.CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.CheckedInPerBranch * 3, await database.AppointmentCheckIns.CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.CheckedInPerBranch * 3, await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.CheckIn).CountAsync());
        Assert.Equal(3, await database.IntakeAppointments.CountAsync(item => item.CancelledAt != null));
        Assert.Equal(3, await database.AppointmentMailOutbox.Where(item => item.Kind == AppointmentMailKind.Cancellation).CountAsync());
        Assert.Equal(DevelopmentIntakeManifest.Appointments.Count * 4, await database.IntakePhotos.CountAsync(photo => photo.AvailableAt != null));
        var schedule = services.GetRequiredService<AppointmentSchedule>();
        var malaysia = DealershipLocale.Malaysia;
        var confirmations = await database.EmailVerificationChallenges.Where(item => item.ConsumedAt != null)
            .ToDictionaryAsync(item => item.UserId, item => item.ConsumedAt!.Value);
        foreach (var appointment in appointments)
        {
            Assert.True(confirmations[appointment.OwnerId] <= appointment.CreatedAt);
            Assert.Equal(new AppointmentSlot(appointment.StartsAt, appointment.EndsAt),
                schedule.FindSlot(appointment.StartsAt, appointment.CreatedAt));
            Assert.Equal(TimeZoneInfo.ConvertTime(appointment.CreatedAt, malaysia).Year,
                appointment.Valuation.ValuationYear);
        }

        await AssertStatesAsync(database);
        var query = new StaffWorklistQuery(appointments.Min(item => item.StartsAt).AddDays(-1),
            appointments.Max(item => item.EndsAt).AddDays(1), StaffWorklistStatus.Booked, null, 1, 25);

        foreach (var staff in DevelopmentFixtureManifest.Staff)
        {
            var store = services.GetRequiredService<IStaffWorklistStore>();

            // A suspended account is inactive, so the worklist is refused to them.
            if (staff.Role is AccountRoles.Administrator or AccountRoles.WorkshopTechnician ||
                staff.Id == DevelopmentLifecycleManifest.SuspendedStaffId)
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
                Assert.Equal(Math.Min(25, expected.Length - actual.Count), result.Items.Count);
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

            if (!customer.VerifyEmail || customer.Registration.Email == DevelopmentLifecycleManifest.SuspendedCustomerEmail)
            {
                Assert.Null(await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, 1, 25), CancellationToken.None));
                continue;
            }

            var actual = new List<Guid>();
            var expected = appointments.Where(item => item.OwnerId == owner.UserId)
                .OrderByDescending(item => item.StartsAt).ThenByDescending(item => item.Id).Select(item => item.Id);

            if (!expected.Any())
            {
                var empty = await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, 1, 25), CancellationToken.None);
                Assert.NotNull(empty);
                Assert.Equal(0, empty.Total);
                Assert.Empty(empty.Items);
                continue;
            }

            for (var page = 1; page <= PageSizes.Length; page++)
            {
                var result = await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, page, 25), CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal(DevelopmentIntakeManifest.TotalPerBranch, result.Total);
                Assert.Equal(PageSizes[page - 1], result.Items.Count);
                actual.AddRange(result.Items.Select(item => item.Id));
            }

            Assert.Equal(expected, actual);
            Assert.Empty((await history.ListAsync(owner.UserId, new AppointmentHistoryQuery(null, null, PageSizes.Length + 1, 25), CancellationToken.None))!.Items);
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
            Assert.Equal(DevelopmentFixtureManifest.CheckInHandlerFor(fixture.BranchId, fixture.Id).Id, checkIn.StaffId);
            var claim = await database.AppointmentAssignments.SingleAsync(item => item.AppointmentId == fixture.Id);
            Assert.Equal(WorkOwnershipKind.Claimed, claim.Kind);
            Assert.Equal(checkIn.StaffId, claim.ActorId);
            Assert.Equal(checkIn.StaffId, claim.HandlerId);
            Assert.Equal(checkIn.CheckedInAt, claim.RecordedAt);
            Assert.Equal(1, claim.Version);
            var historyScenario = DevelopmentInspectionScenarios.All.SingleOrDefault(item => item.Id == fixture.Id);
            if (DevelopmentWorkshopHistory.Covers(fixture) ||
                historyScenario is not null && DevelopmentWorkshopHistory.Covers(historyScenario))
            {
                var signed = await database.InspectionSignedRevisions.AsNoTracking()
                    .SingleAsync(item => item.InspectionId == fixture.Id);
                Assert.Equal(checkIn.CheckedInAt.AddMinutes(80), signed.SignedAt);
                Assert.True(signed.SignedAt < DateTimeOffset.UtcNow);
                if (DevelopmentWorkshopHistory.Covers(fixture))
                {
                    var repair = await database.InspectionRepairRequests.AsNoTracking()
                        .SingleAsync(item => item.InspectionId == fixture.Id);
                    Assert.Equal(signed.SignedAt.AddMinutes(30), repair.RequestedAt);
                }
            }

            var vehicle = await database.Vehicles.SingleAsync(item => item.Id == checkIn.VehicleId);
            var completedBaseline = fixture.Id == DevelopmentInspectionManifest.Baselines[1].Id;
            var scenario = DevelopmentInspectionScenarios.All.SingleOrDefault(item => item.Id == fixture.Id);
            if (scenario is null)
            {
                Assert.Equal(completedBaseline ? VehicleStatus.Available : VehicleStatus.PendingInspection, vehicle.Status);
            }
            Assert.Equal(fixture.Id, vehicle.IntakeAppointmentId);
            Assert.Equal(fixture.BranchId, vehicle.BranchId);
            Assert.Equal(fixture.CheckIn!.MileageKilometres, vehicle.MileageKilometres);
            Assert.Equal(fixture.Vehicle.MileageKilometres,
                (await database.IntakeAppointments.SingleAsync(item => item.Id == fixture.Id)).Valuation.MileageKilometres);
        }

        foreach (var fixture in DevelopmentIntakeManifest.Appointments.Where(item => item.Status == StaffWorklistStatus.Cancelled))
        {
            var appointment = await database.IntakeAppointments.SingleAsync(item => item.Id == fixture.Id);
            Assert.True(appointment.CreatedAt <= appointment.CancelledAt);
            Assert.True(appointment.CancelledAt < appointment.StartsAt);
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
            using var expected = DevelopmentFixturePhotos.Open(angle,
                DevelopmentVehicleCatalogue.For(appointment.Valuation.Make, appointment.Valuation.Model,
                    appointment.Valuation.Trim));
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
            Photos = await database.IntakePhotos.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Attachments = await database.AppointmentPhotos.AsNoTracking().OrderBy(item => item.AppointmentId).ThenBy(item => item.PhotoId).ToArrayAsync(),
            Mail = await database.AppointmentMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            CheckIns = await database.AppointmentCheckIns.AsNoTracking().OrderBy(item => item.AppointmentId).ToArrayAsync(),
            IntakeAssignments = await database.AppointmentAssignments.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            Vehicles = await database.Vehicles.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionDrafts = await database.InspectionDrafts.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionAssignments = await database.InspectionAssignments.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionSaves = await database.InspectionDraftSaves.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionSignedRevisions = await database.InspectionSignedRevisions.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionReopenings = await database.InspectionReopenings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionRepairRequests = await database.InspectionRepairRequests.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionRepairApprovals = await database.InspectionRepairApprovals.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionRepairCompletions = await database.InspectionRepairCompletions.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionPhotos = await database.InspectionPhotos.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync(),
            InspectionDraftPhotos = await database.InspectionDraftPhotos.AsNoTracking().OrderBy(item => item.SaveId).ThenBy(item => item.PhotoId).ToArrayAsync(),
            InspectionPhotoRemovals = await database.InspectionPhotoRemovals.AsNoTracking().OrderBy(item => item.PhotoId).ToArrayAsync()
        });
    }
}
