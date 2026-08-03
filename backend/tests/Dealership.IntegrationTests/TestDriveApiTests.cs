using Dealership.Domain.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Dealership.Application.Accounts;
using Dealership.Application.TestDrives;
using Dealership.Application.WorkCalendar;
using Dealership.Domain.Intake;
using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Dealership.IntegrationTests.ReservationTestData;

namespace Dealership.IntegrationTests;

public sealed class TestDriveApiTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task HttpCommandsRetainReceiptsAndSlotOffersAndHistoryStayScoped()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        using var setup = data.Factory.Services.CreateScope();
        var services = setup.ServiceProvider;
        var database = services.GetRequiredService<DealershipDbContext>();
        var manager = await ProvisionAsync(services, data.BranchId, AccountRoles.BranchManager);
        var technician = await ProvisionAsync(services, data.BranchId, AccountRoles.WorkshopTechnician);
        var generalManager = await ProvisionAsync(services, null, AccountRoles.GeneralManager);
        var listing = await CreateListingAsync(data, services, manager, technician, 0);
        var other = await RegisterCustomerAsync(services, database);
        using var factory = fixture.CreateFactory();
        using var customer = factory.CreateClient();
        using var agent = factory.CreateClient();
        using var branchManager = factory.CreateClient();
        using var unrelated = factory.CreateClient();
        using var groupManager = factory.CreateClient();
        var vehiclePath = $"/api/vehicles/{listing.Revision.VehicleId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/account/test-drives")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", new { })).StatusCode);
        await LoginAsync(customer, data.CustomerEmail);
        await LoginAsync(agent, data.StaffEmail);
        await LoginAsync(branchManager, (await database.Users.Where(user => user.Id == manager).Select(user => user.Email).SingleAsync())!);
        await LoginAsync(unrelated, (await database.Users.Where(user => user.Id == other).Select(user => user.Email).SingleAsync())!);
        await LoginAsync(groupManager, (await database.Users.Where(user => user.Id == generalManager).Select(user => user.Email).SingleAsync())!);
        var now = DateTimeOffset.UtcNow;
        var tomorrow = DateOnly.FromDateTime(now.AddHours(8).DateTime).AddDays(1);
        var schedule = factory.Services.GetRequiredService<AppointmentSchedule>();
        var slots = Enumerable.Range(0, 7).SelectMany(day => schedule.GetSlots(tomorrow.AddDays(day), now,
            TestDriveBooking.Duration)).Take(7).ToArray();
        Assert.Equal(7, slots.Length);
        var date = slots[0].StartsAt.AddHours(8).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var offersPath = $"{vehiclePath}/test-drive-slots?date={date}";
        var offerResponse = await customer.GetAsync(offersPath);
        Assert.Equal(HttpStatusCode.OK, offerResponse.StatusCode);
        Assert.True(offerResponse.Headers.CacheControl?.NoStore);
        var offer = (await offerResponse.Content.ReadFromJsonAsync<TestDriveSlotOffer>())!;
        Assert.Equal(listing.Publication.Id, offer.PublicationId);
        Assert.Contains(offer.Slots, slot => slot.StartsAt == slots[0].StartsAt);
        Assert.All(offer.Slots, slot => Assert.Equal(TestDriveBooking.Duration, slot.EndsAt - slot.StartsAt));
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync($"{vehiclePath}/test-drive-slots?date=0001-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/vehicles/{Guid.NewGuid()}/test-drive-slots?date={date}")).StatusCode);
        var request = new TestDriveBookingRequest(Guid.NewGuid(), offer.PublicationId, slots[0].StartsAt);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsJsonAsync($"{vehiclePath}/test-drives",
            new { request.OperationId, request.PublicationId })).StatusCode);
        using var malformed = new StringContent("{", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsync($"{vehiclePath}/test-drives", malformed)).StatusCode);
        using var oversized = new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await customer.PostAsync($"{vehiclePath}/test-drives", oversized)).StatusCode);
        using var wrongType = new StringContent("{}");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await customer.PostAsync($"{vehiclePath}/test-drives", wrongType)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync($"{vehiclePath}/test-drives", request)).StatusCode);
        var booked = await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", request);
        Assert.Equal(HttpStatusCode.OK, booked.StatusCode);
        Assert.True(booked.Headers.CacheControl?.NoStore);
        var receipt = await booked.Content.ReadAsStringAsync();
        Assert.Equal(receipt, await (await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", request)).Content.ReadAsStringAsync());
        var conflict = await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", request with { StartsAt = slots[1].StartsAt });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("test-drive-operation-conflict", (await conflict.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        var occupied = (await customer.GetFromJsonAsync<TestDriveSlotOffer>(offersPath))!;
        Assert.DoesNotContain(occupied.Slots, slot => slot.StartsAt == request.StartsAt);
        var bookingPath = $"/api/staff/test-drives/{request.OperationId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(bookingPath)).StatusCode);
        var open = (await agent.GetFromJsonAsync<StaffTestDrivePage>("/api/staff/test-drives?queue=toClaim"))!;
        Assert.Equal(data.StaffId, open.Actor.UserId);
        Assert.Contains(open.Items, item => item.Id == request.OperationId && item.AssignmentId == null);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/staff/test-drives")).StatusCode);
        var initialDetail = (await agent.GetFromJsonAsync<StaffTestDriveDetail>(bookingPath))!;
        Assert.Equal("Booked", initialDetail.Booking.Status);
        Assert.Equal(data.CustomerEmail, initialDetail.CustomerEmail);
        Assert.Null(initialDetail.Start);
        var handlers = (await branchManager.GetFromJsonAsync<TestDriveHandlers>($"{bookingPath}/handlers"))!;
        Assert.Contains(handlers.Items, item => item.Id == manager);
        Assert.Contains(handlers.Items, item => item.Id == data.StaffId);
        Assert.DoesNotContain(handlers.Items, item => item.Id == technician);
        Assert.Equal(HttpStatusCode.Forbidden, (await groupManager.GetAsync($"{bookingPath}/handlers")).StatusCode);
        var claim = new TestDriveAssignmentRequest(Guid.NewGuid(), 0);
        var calendarPath = $"/api/staff/work-calendar?from={date}&to={date}";
        Assert.Empty((await agent.GetFromJsonAsync<WorkCalendarSnapshot>(calendarPath))!.Items);
        Assert.Equal(request.OperationId, Assert.Single((await branchManager.GetFromJsonAsync<WorkCalendarSnapshot>(
            $"{calendarPath}&employee=unassigned"))!.Items).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"{bookingPath}/claims", claim)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await groupManager.PostAsJsonAsync($"{bookingPath}/claims", claim)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.PostAsJsonAsync($"{bookingPath}/claims", new { claim.OperationId })).StatusCode);
        var claimed = await agent.PostAsJsonAsync($"{bookingPath}/claims", claim);
        Assert.Equal(HttpStatusCode.OK, claimed.StatusCode);
        await AssertCalendarAsync(agent, branchManager, customer, date, request.OperationId, data, services, manager, generalManager, technician);
        var myWork = (await agent.GetFromJsonAsync<StaffTestDrivePage>("/api/staff/test-drives?queue=myWork"))!;
        Assert.Contains(myWork.Items, item => item.Id == request.OperationId && item.HandlerId == data.StaffId && item.HandlerEligible);
        var handlerPath = $"/api/staff/test-drives?handler={Uri.EscapeDataString(data.StaffEmail.ToUpperInvariant())}&pageSize=10";
        var filtered = (await branchManager.GetFromJsonAsync<StaffTestDrivePage>(handlerPath))!;
        Assert.Equal(request.OperationId, Assert.Single(filtered.Items).Id);
        Assert.Equal(myWork.Counts.All, filtered.Counts.All);
        Assert.Equal(filtered.Total, (await groupManager.GetFromJsonAsync<StaffTestDrivePage>($"{handlerPath}&branchId={data.BranchId}"))!.Total);
        Assert.Empty((await branchManager.GetFromJsonAsync<StaffTestDrivePage>("/api/staff/test-drives?handler=%25"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await branchManager.GetAsync($"/api/staff/test-drives?handler={new string('x', 161)}")).StatusCode);
        var row = filtered.Items[0];
        var columns = $"registration={Uri.EscapeDataString(row.RegistrationPlate.ToLowerInvariant())}" +
            $"&vehicle={Uri.EscapeDataString(row.Model.ToLowerInvariant())}" +
            $"&customer={Uri.EscapeDataString(row.CustomerName.ToLowerInvariant())}";
        Assert.Contains((await branchManager.GetFromJsonAsync<StaffTestDrivePage>($"/api/staff/test-drives?{columns}"))!.Items,
            item => item.Id == row.Id);
        Assert.Empty((await branchManager.GetFromJsonAsync<StaffTestDrivePage>("/api/staff/test-drives?customer=%25"))!.Items);
        var assignment = (await claimed.Content.ReadFromJsonAsync<JsonObject>())!["receipt"]!.AsObject();
        var stale = await agent.PostAsJsonAsync($"{bookingPath}/claims", claim with { OperationId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(data.StaffId, (await stale.Content.ReadFromJsonAsync<JsonObject>())!["current"]!["handlerId"]!.GetValue<Guid>());
        var assign = new TestDriveAssignmentRequest(Guid.NewGuid(), 1, manager, "Cover customer arrival");
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.PostAsJsonAsync($"{bookingPath}/assignments", assign)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await branchManager.PostAsJsonAsync($"{bookingPath}/assignments", assign)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync(bookingPath)).StatusCode);
        Assert.Empty((await agent.GetFromJsonAsync<WorkCalendarSnapshot>(calendarPath))!.Items);
        var assigned = (await groupManager.GetFromJsonAsync<StaffTestDriveDetail>(bookingPath))!;
        Assert.Equal(manager, assigned.Booking.HandlerId);
        Assert.Equal(assign.OperationId, assigned.Booking.AssignmentId);
        Assert.Equal(2, assigned.Booking.OwnershipVersion);
        var replay = (await (await agent.PostAsJsonAsync($"{bookingPath}/claims", claim)).Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(assignment.ToJsonString(), replay["receipt"]!.ToJsonString());
        Assert.Equal(manager, replay["current"]!["handlerId"]!.GetValue<Guid>());
        await AssertHandlingBindingAsync(branchManager, groupManager, bookingPath, assign.OperationId);
        Assert.Equal(HttpStatusCode.OK, (await branchManager.PostAsJsonAsync($"{bookingPath}/releases",
            new TestDriveAssignmentRequest(Guid.NewGuid(), 2))).StatusCode);
        var cancellation = new { OperationId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.NotFound, (await unrelated.PostAsJsonAsync($"/api/test-drives/{request.OperationId}/cancellations", cancellation)).StatusCode);
        await database.Users.Where(user => user.Id == data.OwnerId).ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", request)).StatusCode);
        var cancelled = await customer.PostAsJsonAsync($"/api/test-drives/{request.OperationId}/cancellations", cancellation);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var cancelledReceipt = await cancelled.Content.ReadAsStringAsync();
        Assert.Equal(cancelledReceipt, await (await customer.PostAsJsonAsync($"/api/test-drives/{request.OperationId}/cancellations", cancellation)).Content.ReadAsStringAsync());
        var reopened = (await customer.GetFromJsonAsync<TestDriveSlotOffer>(offersPath))!;
        Assert.Contains(reopened.Slots, slot => slot.StartsAt == request.StartsAt);
        Assert.Equal(1, (await customer.GetFromJsonAsync<TestDriveHistory>("/api/account/test-drives"))!.Total);
        await database.Users.Where(user => user.Id == data.OwnerId).ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, true));
        foreach (var slot in slots.Skip(1))
        {
            var next = request with { OperationId = Guid.NewGuid(), StartsAt = slot.StartsAt };
            Assert.Equal(HttpStatusCode.OK, (await customer.PostAsJsonAsync($"{vehiclePath}/test-drives", next)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await branchManager.PostAsJsonAsync(
                $"/api/staff/test-drives/{next.OperationId}/cancellations", new { OperationId = Guid.NewGuid() })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await branchManager.PostAsJsonAsync(
                $"/api/staff/test-drives/{next.OperationId}/cancellations", new { OperationId = Guid.NewGuid(), Reason = "Customer requested another date" })).StatusCode);
        }

        await AssertHistoryAsync(customer, unrelated, agent, date, services, data.OwnerId, other);
        await AssertStaffReadsAsync(branchManager, groupManager, agent, date, request.OperationId, data.BranchId);
        var reads = services.GetRequiredService<IStaffTestDriveReadStore>();
        Assert.Equal(TestDriveReadStatus.Forbidden, (await reads.GetAsync(technician, request.OperationId, default)).Status);
        await database.Users.Where(user => user.Id == manager).ExecuteUpdateAsync(update => update.SetProperty(user => user.IsActive, false));
        Assert.Equal(TestDriveReadStatus.Forbidden, (await reads.GetAsync(manager, request.OperationId, default)).Status);
    }

    private static async Task AssertCalendarAsync(HttpClient agent, HttpClient manager, HttpClient customer,
        string date, Guid bookingId, CheckInTestData data, IServiceProvider services, Guid managerId, Guid groupId,
        Guid technicianId)
    {
        var path = $"/api/staff/work-calendar?from={date}&to={date}";
        var response = await agent.GetAsync(path);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var own = (await response.Content.ReadFromJsonAsync<WorkCalendarSnapshot>())!;
        Assert.Equal(data.StaffId, own.Actor.UserId);
        var drive = Assert.Single(own.Items, item => item.Kind == "TestDrive");
        Assert.Equal(bookingId, drive.Id);
        Assert.Equal(data.StaffId, drive.HandlerId);
        Assert.Equal(data.BranchId, own.BranchId);
        Assert.All(own.Items, item => Assert.Equal(data.StaffId, item.HandlerId));
        var reads = services.GetRequiredService<IWorkCalendarStore>();
        var query = WorkCalendarQuery.Parse(date, date, null, null)!;
        var refined = (await reads.ScheduledAsync(managerId, query with
        {
            Scope = new(null, data.StaffEmail.ToUpperInvariant())
        }, default)).Value!;
        Assert.Equal(own.Items.Select(item => item.Id), refined.Items.Select(item => item.Id));
        Assert.Empty((await reads.ScheduledAsync(managerId, query with { Scope = new(null, "unassigned") }, default)).Value!.Items);
        Assert.Empty((await reads.ScheduledAsync(managerId, query with { Scope = new(null, "%") }, default)).Value!.Items);
        var allBranches = (await reads.ScheduledAsync(groupId, query, default)).Value!;
        Assert.Null(allBranches.BranchId);
        Assert.Equal(bookingId, Assert.Single(allBranches.Items).Id);
        Assert.Equal(bookingId, Assert.Single((await reads.ScheduledAsync(groupId,
            query with { Scope = new(data.BranchId) }, default)).Value!.Items).Id);
        Assert.Equal(WorkCalendarReadStatus.Forbidden,
            (await reads.ScheduledAsync(technicianId, query, default)).Status);
        Assert.Equal(bookingId, Assert.Single((await reads.ScheduledAsync(managerId,
            query with { Search = data.StaffEmail.ToUpperInvariant(), Assignment = "Assigned" }, default)).Value!.Items).Id);
        Assert.Empty((await reads.ScheduledAsync(managerId,
            query with { Assignment = "Unassigned" }, default)).Value!.Items);
        Assert.Equal(WorkCalendarReadStatus.Forbidden, (await reads.ScheduledAsync(data.StaffId,
            query with { Scope = new(null, "unassigned") }, default)).Status);
        Assert.Equal(WorkCalendarReadStatus.Forbidden, (await reads.ScheduledAsync(managerId,
            query with { Scope = new(Guid.NewGuid()) }, default)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/staff/work-calendar?from=2026-11-01&to=2026-12-13")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync($"{path}&employee={new string('x', 161)}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/staff/work-calendar/unscheduled?pageSize=5")).StatusCode);
        Assert.Empty((await agent.GetFromJsonAsync<UnscheduledWorkPage>("/api/staff/work-calendar/unscheduled"))!.Items);
    }
    private static async Task AssertStaffReadsAsync(HttpClient manager, HttpClient group, HttpClient agent,
        string date, Guid bookingId, Guid branchId)
    {
        var path = $"/api/staff/test-drives?from={date}&to={date}&pageSize=10&sort=startsAt&direction=desc";
        var response = await manager.GetAsync(path);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = (await response.Content.ReadFromJsonAsync<StaffTestDrivePage>())!;
        Assert.Equal(7, page.Total);
        Assert.Equal(7, page.Counts.Cancelled);
        Assert.All(page.Items, item => Assert.Equal("Cancelled", item.Status));
        var cancelled = (await manager.GetFromJsonAsync<StaffTestDrivePage>($"{path}&status=cancelled"))!;
        Assert.Equal(page.Total, cancelled.Total);
        Assert.All(cancelled.Items, item => Assert.Equal(TestDriveStatus.Cancelled, item.Status));
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync($"{path}&status=Cancelled")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await agent.GetAsync("/api/staff/test-drives?queue=MyWork")).StatusCode);
        Assert.Equal(page.Items.OrderByDescending(item => item.StartsAt).ThenBy(item => item.Id).Select(item => item.Id),
            page.Items.Select(item => item.Id));
        Assert.Empty((await manager.GetFromJsonAsync<StaffTestDrivePage>($"{path}&page={int.MaxValue}"))!.Items);
        var searched = (await manager.GetFromJsonAsync<StaffTestDrivePage>($"{path}&search={bookingId}"))!;
        Assert.Equal(bookingId, Assert.Single(searched.Items).Id);
        Assert.Equal(page.Total, searched.Counts.All);
        Assert.Equal(page.Total, (await group.GetFromJsonAsync<StaffTestDrivePage>($"{path}&branchId={branchId}"))!.Total);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"{path}&branchId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync($"/api/staff/test-drives?from={date}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/staff/test-drives?pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/staff/test-drives?sort=unknown")).StatusCode);
        Assert.Equal(0, (await agent.GetFromJsonAsync<StaffTestDrivePage>("/api/staff/test-drives?queue=history"))!.Total);
    }

    private static async Task AssertHandlingBindingAsync(HttpClient handler, HttpClient denied, string path, Guid assignmentId)
    {
        var start = new TestDriveStartRequest(Guid.NewGuid(), assignmentId, true, true);
        Assert.Equal(HttpStatusCode.BadRequest, (await handler.PostAsJsonAsync($"{path}/starts",
            new { start.OperationId, start.AssignmentId, start.CustomerPresent })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await handler.PostAsJsonAsync($"{path}/starts",
            start with { DrivingLicenceChecked = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync($"{path}/starts", start)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await handler.PostAsJsonAsync($"{path}/starts", start)).StatusCode);
        var outcome = new TestDriveOutcomeRequest(Guid.NewGuid(), assignmentId, TestDriveOutcomeKind.NotCompleted,
            true, "Licence unavailable");
        Assert.Equal(HttpStatusCode.BadRequest, (await handler.PostAsJsonAsync($"{path}/outcomes",
            new { outcome.OperationId, outcome.AssignmentId, outcome.Kind })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync($"{path}/outcomes", outcome)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await handler.PostAsJsonAsync($"{path}/outcomes", outcome)).StatusCode);
    }

    private static async Task AssertHistoryAsync(HttpClient customer, HttpClient unrelated, HttpClient staff, string date,
        IServiceProvider services, Guid customerId, Guid unrelatedId)
    {
        var first = (await customer.GetFromJsonAsync<TestDriveHistory>("/api/account/test-drives"))!;
        Assert.Equal(7, first.Total);
        Assert.Equal(5, first.Items.Count);
        Assert.Equal(5, first.PageSize);
        Assert.All(first.Items, item => Assert.Equal("Cancelled", item.Status));
        var last = (await customer.GetFromJsonAsync<TestDriveHistory>("/api/account/test-drives?page=2"))!;
        Assert.Equal(2, last.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(last.Items.Select(item => item.Id)));
        Assert.Empty((await customer.GetFromJsonAsync<TestDriveHistory>($"/api/account/test-drives?page={int.MaxValue}"))!.Items);
        Assert.Equal(7, (await customer.GetFromJsonAsync<TestDriveHistory>($"/api/account/test-drives?from={date}&to={date}"))!.Total);
        var reference = last.Items[0].Id;
        var reads = services.GetRequiredService<ITestDriveReadStore>();
        var dates = Dealership.Application.Common.ListDateRange.Parse(null, null)!;
        var searched = (await reads.AccountAsync(customerId, 1, dates, default, reference.ToString())).History!;
        Assert.Equal(1, searched.Total);
        Assert.Equal(reference, Assert.Single(searched.Items).Id);
        Assert.Equal(0, (await reads.AccountAsync(unrelatedId, 1, dates, default, reference.ToString())).History!.Total);
        Assert.Equal(0, (await reads.AccountAsync(customerId, 1, dates, default, "%")).History!.Total);
        Assert.Equal(0, (await unrelated.GetFromJsonAsync<TestDriveHistory>("/api/account/test-drives"))!.Total);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/account/test-drives")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync("/api/account/test-drives?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync($"/api/account/test-drives?from={date}")).StatusCode);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(Guid.NewGuid(), email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }
}
