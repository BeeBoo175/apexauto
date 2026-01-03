using System.Globalization;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;

namespace Dealership.UnitTests;

public sealed class IntakeCheckInTests
{
    private static readonly DateTimeOffset StartsAt = DateTimeOffset.Parse("2026-10-16T01:00:00Z", CultureInfo.InvariantCulture);
    private static readonly CheckInEvidence Evidence = new("pl1bt65r84b123456", 92000, true, true, true, true, "  Owner present  ");

    [Theory]
    [InlineData("2026-10-15T15:59:59.9999999Z", false)]
    [InlineData("2026-10-15T16:00:00Z", true)]
    [InlineData("2026-10-16T00:00:00Z", true)]
    [InlineData("2026-10-16T15:59:59.9999999Z", true)]
    [InlineData("2026-10-16T16:00:00Z", false)]
    public void CheckInUsesTheWholeBookedMalaysianCalendarDate(string time, bool allowed)
    {
        var appointment = Appointment();
        var now = DateTimeOffset.Parse(time, CultureInfo.InvariantCulture);

        if (!allowed)
        {
            Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(), Evidence, now));
            return;
        }

        var receipt = new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(), Evidence, now);
        Assert.Equal(now, receipt.CheckedInAt);
    }

    [Fact]
    public void ActualMileageAndPhysicalEvidenceDoNotRewriteTheBookingQuote()
    {
        var appointment = Appointment();
        var valuation = new SellerValuation(appointment.Id, appointment.OwnerId,
            new ValuedVehicle("Proton", "Saga", "Standard", 2020, 90000, 18000, 23000, "MYR", 2026));
        var checkIn = new AppointmentCheckIn(Guid.NewGuid(), appointment, Guid.NewGuid(), Evidence, StartsAt);
        var vehicle = new Vehicle(appointment, valuation, checkIn);

        Assert.Equal("PL1BT65R84B123456", vehicle.Vin);
        Assert.Equal("WYY8821", vehicle.RegistrationIdentity);
        Assert.Equal(VehicleStatus.PendingInspection, vehicle.Status);
        Assert.Equal(92000, vehicle.MileageKilometres);
        Assert.Equal(90000, valuation.MileageKilometres);
        Assert.Equal(18000, valuation.Minimum);
        Assert.Equal(23000, valuation.Maximum);
        Assert.Equal(appointment.Id, vehicle.IntakeAppointmentId);
        Assert.Equal(appointment.OwnerId, vehicle.IntakeOwnerId);
        Assert.Equal(appointment.BranchId, vehicle.BranchId);
        Assert.Equal("Owner present", checkIn.Notes);
        Assert.True(checkIn.Matches(Evidence with { Vin = " PL1BT65R84B123456 " }));
        Assert.False(checkIn.Matches(Evidence with { MileageKilometres = 91999 }));
        Assert.False(checkIn.Matches(Evidence with { Notes = "different physical finding" }));
        Assert.Throws<ArgumentException>(() => new Vehicle(Appointment(), valuation, checkIn));
    }

    [Theory]
    [InlineData("vin")]
    [InlineData("ambiguous-vin")]
    [InlineData("unicode-vin")]
    [InlineData("odometer")]
    [InlineData("presence")]
    [InlineData("identity")]
    [InlineData("ownership")]
    [InlineData("vehicle")]
    [InlineData("notes")]
    public void PhysicalEvidenceMustBeCompleteAndBounded(string invalidField)
    {
        var invalid = invalidField switch
        {
            "vin" => Evidence with { Vin = "" },
            "ambiguous-vin" => Evidence with { Vin = "PL1BT65R84B12345O" },
            "unicode-vin" => Evidence with { Vin = "PL1BT65R84B12345\u0661" },
            "odometer" => Evidence with { MileageKilometres = -1 },
            "presence" => Evidence with { CustomerPresent = false },
            "identity" => Evidence with { IdentityConfirmed = false },
            "ownership" => Evidence with { OwnershipConfirmed = false },
            "vehicle" => Evidence with { VehicleConfirmed = false },
            _ => Evidence with { Notes = new string('x', CheckInEvidence.NotesMaximumLength + 1) }
        };

        Assert.Throws<ArgumentException>(() => invalid.Normalize());
    }

    [Fact]
    public void ZeroOdometerAndBoundedNotesAreValidButMissingActorsAreRefused()
    {
        var normalized = (Evidence with { MileageKilometres = 0, Notes = new string('x', CheckInEvidence.NotesMaximumLength) }).Normalize();
        Assert.Equal(0, normalized.MileageKilometres);
        Assert.Equal(CheckInEvidence.NotesMaximumLength, normalized.Notes!.Length);
        Assert.Null((Evidence with { Notes = "  " }).Normalize().Notes);
        Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.Empty, Appointment(), Guid.NewGuid(), Evidence, StartsAt));
        Assert.Throws<ArgumentException>(() => new AppointmentCheckIn(Guid.NewGuid(), Appointment(), Guid.Empty, Evidence, StartsAt));
    }

    private static IntakeAppointment Appointment()
    {
        return new IntakeAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "WYY 8821",
            new InspectionSlot(StartsAt, StartsAt.AddMinutes(90)), StartsAt.AddDays(-1));
    }
}
