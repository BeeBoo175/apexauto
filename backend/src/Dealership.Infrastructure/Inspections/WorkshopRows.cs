using Dealership.Application.Accounts;
using Dealership.Application.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Inspections;

internal static class WorkshopRows
{
    public static IQueryable<WorkshopRow> Create(DealershipDbContext database, AccountUser actor, Guid? selectedBranch)
    {
        var branchId = actor.BranchId ?? selectedBranch;
        var covered = InspectionRepairAuthority.CoveredRequests(database);
        var staff = PersonRows.Staff(database);
        return from vehicle in database.Vehicles.AsNoTracking()
               join branch in database.Branches.AsNoTracking() on vehicle.BranchId equals branch.Id
               join inspection in database.InspectionDrafts.AsNoTracking() on vehicle.Id equals inspection.VehicleId into inspections
               from draft in inspections.DefaultIfEmpty()
               join assigned in database.Users.AsNoTracking() on draft.TechnicianId equals assigned.Id into technicians
               from technician in technicians.DefaultIfEmpty()
               from request in database.InspectionRepairRequests.AsNoTracking().Where(item =>
                   draft != null && draft.IsSigned && item.InspectionId == draft.Id && item.Revision == draft.Revision &&
                   item.AssignmentVersion == draft.AssignmentVersion && item.Version == draft.RepairVersion).DefaultIfEmpty()
               where (branchId == null || vehicle.BranchId == branchId) &&
                   (actor.Role != AccountRoles.WorkshopTechnician || draft.TechnicianId == actor.Id ||
                       draft == null && vehicle.Status == VehicleStatus.PendingInspection)
               select new WorkshopRow
               {
                   VehicleId = vehicle.Id,
                   BranchId = vehicle.BranchId,
                   BranchName = branch.Name,
                   RegistrationPlate = vehicle.RegistrationPlate,
                   Vin = vehicle.Vin,
                   Make = vehicle.Make,
                   Model = vehicle.Model,
                   ManufactureYear = vehicle.ManufactureYear,
                   Trim = vehicle.Trim,
                   CheckedInAt = vehicle.CreatedAt,
                   RequestedAt = request == null ? null : request.RequestedAt,
                   VehicleStatus = vehicle.Status,
                   InspectionId = draft == null ? null : draft.Id,
                   TechnicianId = draft == null ? null : draft.TechnicianId,
                   TechnicianIdentity = technician == null ? null : staff
                       .Where(person => person.UserId == technician.Id)
                       .Select(person => person.Name).FirstOrDefault(),
                   TechnicianEmail = technician == null ? null : technician.Email,
                   TechnicianPictureId = technician == null ? null : staff
                       .Where(person => person.UserId == technician.Id)
                       .Select(person => person.PictureId).FirstOrDefault(),
                   Revision = draft == null ? null : draft.Revision,
                   IsSigned = draft != null && draft.IsSigned,
                   RequestId = request == null ? null : request.Id,
                   Budget = request == null ? null : request.Budget,
                   Since = request == null ? vehicle.CreatedAt : request.RequestedAt,
                   Completed = request != null && database.InspectionRepairCompletions.Any(item => item.RequestId == request.Id),
                   Approved = request != null && covered.Any(item => item.Request.Id == request.Id)
               };
    }

    public static IQueryable<WorkshopRow> ForTask(DealershipDbContext database, AccountUser actor, Guid? selectedBranch, WorkshopTask task)
    {
        var work = WorkshopRows.Create(database, actor, selectedBranch);
        return task switch
        {
            WorkshopTask.Active => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection),
            WorkshopTask.Inspection => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection && !row.IsSigned),
            WorkshopTask.Approval => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection &&
                row.IsSigned && row.RequestId != null && !row.Completed && row.Budget > 0 && !row.Approved),
            WorkshopTask.Repairs => work.Where(row => row.VehicleStatus == VehicleStatus.PendingInspection &&
                row.IsSigned && (row.RequestId == null || row.Completed || row.Budget == 0 || row.Approved)),
            WorkshopTask.MyWork => work.Where(row => row.TechnicianId == actor.Id &&
                row.VehicleStatus == VehicleStatus.PendingInspection &&
                (!row.IsSigned || row.RequestId == null || row.Completed || row.Budget == 0 || row.Approved)),
            WorkshopTask.Waiting => work.Where(row => row.TechnicianId == actor.Id &&
                row.VehicleStatus == VehicleStatus.PendingInspection && row.IsSigned &&
                row.RequestId != null && !row.Completed && row.Budget > 0 && !row.Approved),
            WorkshopTask.Claimable => work.Where(row => row.InspectionId == null &&
                row.VehicleStatus == VehicleStatus.PendingInspection),
            _ => work
        };
    }

    public static WorkshopWorkStatus Status(WorkshopRow row)
    {
        // Availability is decided by the canonical completion/sign-off services, including partial scope.
        if (row.VehicleStatus == VehicleStatus.Available)
        {
            return WorkshopWorkStatus.ReadyForSale;
        }
        if (row.VehicleStatus == VehicleStatus.Rejected)
        {
            return WorkshopWorkStatus.Rejected;
        }
        if (row.InspectionId is null)
        {
            return WorkshopWorkStatus.AwaitingInspection;
        }
        if (!row.IsSigned)
        {
            return WorkshopWorkStatus.InspectionUnderway;
        }
        if (row.RequestId is null || row.Completed)
        {
            return WorkshopWorkStatus.RepairRequestRequired;
        }
        if (row.Budget > 0 && !row.Approved)
        {
            return WorkshopWorkStatus.ApprovalRequired;
        }

        return WorkshopWorkStatus.RepairsToComplete;
    }

    internal sealed class WorkshopRow
    {
        public Guid VehicleId { get; init; }
        public Guid BranchId { get; init; }
        public string BranchName { get; init; } = string.Empty;
        public string RegistrationPlate { get; init; } = string.Empty;
        public string Vin { get; init; } = string.Empty;
        public string Make { get; init; } = string.Empty;
        public string Model { get; init; } = string.Empty;
        public int ManufactureYear { get; init; }
        public string Trim { get; init; } = string.Empty;
        public DateTimeOffset CheckedInAt { get; init; }
        public DateTimeOffset? RequestedAt { get; init; }
        public VehicleStatus VehicleStatus { get; init; }
        public Guid? InspectionId { get; init; }
        public Guid? TechnicianId { get; init; }
        public string? TechnicianIdentity { get; init; }
        public string? TechnicianEmail { get; init; }
        public Guid? TechnicianPictureId { get; init; }
        public long? Revision { get; init; }
        public bool IsSigned { get; init; }
        public Guid? RequestId { get; init; }
        public decimal? Budget { get; init; }
        public DateTimeOffset Since { get; init; }
        public bool Completed { get; init; }
        public bool Approved { get; init; }
    }

}
