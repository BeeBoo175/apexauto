using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
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

    public DbSet<SellerValuation> SellerValuations => Set<SellerValuation>();

    public DbSet<AppointmentPhoto> AppointmentPhotos => Set<AppointmentPhoto>();

    public DbSet<AppointmentConfirmation> AppointmentConfirmations => Set<AppointmentConfirmation>();

    public DbSet<AppointmentCancellation> AppointmentCancellations => Set<AppointmentCancellation>();

    public DbSet<AppointmentCancellationMail> AppointmentCancellationMails => Set<AppointmentCancellationMail>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<AppointmentCheckIn> AppointmentCheckIns => Set<AppointmentCheckIn>();

    public DbSet<AppointmentCheckInMail> AppointmentCheckInMails => Set<AppointmentCheckInMail>();

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
        builder.ApplyConfiguration<SellerValuation>(booking);
        builder.ApplyConfiguration<AppointmentPhoto>(booking);
        builder.ApplyConfiguration<AppointmentConfirmation>(booking);
        var cancellation = new AppointmentCancellationConfiguration();
        builder.ApplyConfiguration<AppointmentCancellation>(cancellation);
        builder.ApplyConfiguration<AppointmentCancellationMail>(cancellation);
        builder.ApplyConfiguration(new VehicleConfiguration());
        var checkIn = new AppointmentCheckInConfiguration();
        builder.ApplyConfiguration<AppointmentCheckIn>(checkIn);
        builder.ApplyConfiguration<AppointmentCheckInMail>(checkIn);
        builder.ApplyConfiguration(new CustomerProfileConfiguration());
        builder.ApplyConfiguration(new AccountSessionConfiguration());
        builder.ApplyConfiguration(new EmailVerificationChallengeConfiguration());
        builder.ApplyConfiguration(new PasswordResetChallengeConfiguration());
    }
}
