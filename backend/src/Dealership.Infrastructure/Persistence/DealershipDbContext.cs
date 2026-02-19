using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Inspections;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

public class DealershipDbContext(DbContextOptions<DealershipDbContext> options) : IdentityUserContext<AccountUser, Guid>(options)
{
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<IntakePhoto> IntakePhotos => Set<IntakePhoto>();

    public DbSet<PhotoRemoval> PhotoRemovals => Set<PhotoRemoval>();

    public DbSet<IntakeAppointment> IntakeAppointments => Set<IntakeAppointment>();

    public DbSet<AppointmentPhoto> AppointmentPhotos => Set<AppointmentPhoto>();

    public DbSet<AppointmentMail> AppointmentMailOutbox => Set<AppointmentMail>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<InspectionDraft> InspectionDrafts => Set<InspectionDraft>();

    public DbSet<InspectionAssignment> InspectionAssignments => Set<InspectionAssignment>();

    public DbSet<InspectionDraftSave> InspectionDraftSaves => Set<InspectionDraftSave>();

    public DbSet<InspectionSignedRevision> InspectionSignedRevisions => Set<InspectionSignedRevision>();

    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();

    public DbSet<InspectionDraftPhoto> InspectionDraftPhotos => Set<InspectionDraftPhoto>();

    public DbSet<InspectionPhotoRemoval> InspectionPhotoRemovals => Set<InspectionPhotoRemoval>();

    public DbSet<AppointmentCheckIn> AppointmentCheckIns => Set<AppointmentCheckIn>();

    public DbSet<AccountSession> AccountSessions => Set<AccountSession>();

    public DbSet<EmailVerificationChallenge> EmailVerificationChallenges => Set<EmailVerificationChallenge>();

    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new AccountUserConfiguration());
        builder.ApplyConfiguration(new BranchConfiguration());
        builder.ApplyConfiguration(new IntakePhotoConfiguration());
        builder.ApplyConfiguration(new PhotoRemovalConfiguration());
        var booking = new IntakeBookingConfiguration();
        builder.ApplyConfiguration<IntakeAppointment>(booking);
        builder.ApplyConfiguration<AppointmentPhoto>(booking);
        builder.ApplyConfiguration(new AppointmentMailConfiguration());
        builder.ApplyConfiguration(new VehicleConfiguration());
        var inspectionAssignments = new InspectionAssignmentConfiguration();
        builder.ApplyConfiguration<InspectionDraft>(inspectionAssignments);
        builder.ApplyConfiguration<InspectionAssignment>(inspectionAssignments);
        builder.ApplyConfiguration(new InspectionDraftSaveConfiguration());
        builder.ApplyConfiguration(new InspectionSignedRevisionConfiguration());
        builder.ApplyConfiguration(new InspectionPhotoConfiguration());
        var inspectionMedia = new InspectionPhotoAttachmentConfiguration();
        builder.ApplyConfiguration<InspectionDraftPhoto>(inspectionMedia);
        builder.ApplyConfiguration<InspectionPhotoRemoval>(inspectionMedia);
        builder.ApplyConfiguration(new AppointmentCheckInConfiguration());
        builder.ApplyConfiguration(new CustomerProfileConfiguration());
        builder.ApplyConfiguration(new AccountSessionConfiguration());
        builder.ApplyConfiguration(new EmailVerificationChallengeConfiguration());
        builder.ApplyConfiguration(new PasswordResetChallengeConfiguration());
    }
}
