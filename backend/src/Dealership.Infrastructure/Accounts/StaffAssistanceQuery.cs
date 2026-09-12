using Dealership.Application.Accounts;
using Dealership.Domain.Common;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.TestDrives;
using Dealership.Infrastructure.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class StaffAssistanceQuery(DealershipDbContext database) : IStaffAssistanceQuery
{
    public async Task<AssistingStaff?> FindStaffAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var staff = await database.Users.AsNoTracking()
            .Where(user => user.Id == staffId && user.IsActive)
            .Select(ActorRows.Current)
            .SingleOrDefaultAsync(cancellationToken);
        return staff is not null && StaffAssistanceRoles.Allows(staff.Role)
            ? new AssistingStaff(staff.UserId, staff.Role, staff.BranchId)
            : null;
    }

    public async Task<bool> IsClosedAsync(Guid customerId, CancellationToken cancellationToken)
    {
        return await database.Users.AsNoTracking()
            .AnyAsync(user => user.Id == customerId && user.ClosedAt != null, cancellationToken);
    }

    public async Task<CustomerLookupResult> SearchCustomersAsync(Guid staffId, CustomerLookupQuery query,
        CancellationToken cancellationToken)
    {
        if (await FindStaffAsync(staffId, cancellationToken) is null)
        {
            return new(StaffAccountReadStatus.Forbidden);
        }

        var pattern = LiteralSearch.Pattern(query.Search);
        var now = await database.DatabaseNowAsync(cancellationToken);
        var today = AppointmentDay.StartUtc(now);
        var reservation = query.Purpose == CustomerLookupPurpose.Reservation;
        var testDrive = query.Purpose == CustomerLookupPurpose.TestDrive;
        var heldCustomers = ReservationHoldRows.HeldCustomers(database, now);
        var outstandingCustomers = TestDriveOccupationRows.CustomersWithOutstanding(database, today);
        var rows = await (from user in database.Users.AsNoTracking()
                          where user.Role == AccountRoles.Customer && user.IsActive && user.EmailConfirmed &&
                              user.SuspendedAt == null &&
                              // One active hold per customer, and a deposit request waiting for them counts as one.
                              (!reservation || !heldCustomers.Contains(user.Id)) &&
                              // One outstanding test drive per customer, by the rule the booking itself applies.
                              (!testDrive || !outstandingCustomers.Contains(user.Id))
                          join profile in database.CustomerProfiles.AsNoTracking() on user.Id equals profile.UserId
                          // Only active customers are listed, and an active customer is never closed, so the name,
                          // email and mobile are recorded.
                          where EF.Functions.ILike(profile.LegalName!, pattern, "!") ||
                              EF.Functions.ILike(user.Email!, pattern, "!") ||
                              EF.Functions.ILike(profile.Mobile!, pattern, "!")
                          orderby profile.LegalName, user.Id
                          select new { user.Id, LegalName = profile.LegalName!, Email = user.Email!, Mobile = profile.Mobile! })
            .Take(CustomerLookupQuery.MaximumResults).ToListAsync(cancellationToken);
        var items = rows.Select(row => new CustomerLookupRow(row.Id, row.LegalName,
            PersonalDataMasking.Email(row.Email), PersonalDataMasking.Mobile(row.Mobile))).ToArray();
        return new(StaffAccountReadStatus.Succeeded, items);
    }
}
