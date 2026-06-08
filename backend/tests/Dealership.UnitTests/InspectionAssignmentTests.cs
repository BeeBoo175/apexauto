using Dealership.Domain.Common;
using Dealership.Domain.Inspections;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class InspectionAssignmentTests
{
    [Fact]
    public void ReassignmentRequiresCurrentVersionAndRetainsClaimEvidence()
    {
        var technician = Guid.NewGuid();
        var next = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);
        var draft = Draft(technician, at);
        var claim = new InspectionAssignment(draft.Id, draft, technician, null, 0, null, at);
        Assert.Throws<ArgumentException>(() => draft.Reassign(technician, 1));
        Assert.Throws<ArgumentException>(() => draft.Reassign(next, 0));
        Assert.Throws<ArgumentException>(() => draft.Reassign(Guid.Empty, 1));
        draft.Reassign(next, 1);
        var change = new InspectionAssignment(Guid.NewGuid(), draft, Guid.NewGuid(), technician, 1, " Workshop handover ", at);
        Assert.Equal(2, change.Version);
        Assert.Equal("Workshop handover", change.Reason);
        Assert.Equal(technician, claim.TechnicianId);
        Assert.Equal(1, claim.Version);
        Assert.Equal(next, draft.TechnicianId);
    }

    [Fact]
    public void AssignmentEvidenceRequiresCompleteConsistentClaimOrReassignment()
    {
        var technician = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero);
        var draft = Draft(technician, at);
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.Empty, draft, technician, null, 0, null, at));
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.NewGuid(), draft, Guid.NewGuid(), null, 0, null, at));
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.NewGuid(), draft, technician, null, 0, "Reason", at));
        draft.Reassign(Guid.NewGuid(), 1);
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.NewGuid(), draft, technician, technician, 1, null, at));
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.NewGuid(), draft, technician, null, 1, "Reason", at));
        Assert.Throws<ArgumentException>(() => new InspectionAssignment(Guid.NewGuid(), draft, technician, technician, 0, "Reason", at));
        Assert.Throws<ArgumentException>(() => InspectionAssignment.NormalizeReason("line\nbreak"));
        Assert.Throws<ArgumentException>(() => InspectionAssignment.NormalizeReason(new string('x', 1001)));
    }

    private static InspectionDraft Draft(Guid technician, DateTimeOffset at)
    {
        var valuation = new SellerValuation("Proton", "Saga", "Standard", 2020, 90000, 18000, 23000, "MYR", 2026);
        var appointment = new IntakeAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WYY 8821",
            new AppointmentSlot(at, at.AddMinutes(90)), at.AddDays(-1), valuation);
        var evidence = new CheckInEvidence("PL1BT65R84B123456", 92000, true, true, true, true, null);
        var checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(), evidence, at);
        return new InspectionDraft(Guid.NewGuid(), new Vehicle(appointment, checkIn), technician, at);
    }
}
