using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class InspectionRepairAuthority
{
    public static IQueryable<Coverage> CoveredRequests(DealershipDbContext database)
    {
        return from request in database.InspectionRepairRequests.AsNoTracking()
               join scope in database.InspectionRepairRequests.AsNoTracking()
                   on new { request.InspectionId, request.Revision, request.ItemsJson }
                   equals new { scope.InspectionId, scope.Revision, scope.ItemsJson }
               join approval in database.InspectionRepairApprovals.AsNoTracking() on scope.Id equals approval.RequestId
               where scope.Budget >= request.Budget
               select new Coverage { Request = request, Approval = approval };
    }

    internal sealed class Coverage
    {
        public required InspectionRepairRequestRecord Request { get; init; }
        public required InspectionRepairApproval Approval { get; init; }
    }
}
