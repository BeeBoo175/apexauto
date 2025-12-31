using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

public class DealershipDbContext(DbContextOptions<DealershipDbContext> options) : IdentityUserContext<AccountUser, Guid>(options)
{
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<IntakePhoto> IntakePhotos => Set<IntakePhoto>();

    public DbSet<AccountSession> AccountSessions => Set<AccountSession>();

    public DbSet<EmailVerificationChallenge> EmailVerificationChallenges => Set<EmailVerificationChallenge>();

    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new AccountUserConfiguration());
        builder.ApplyConfiguration(new BranchConfiguration());
        builder.ApplyConfiguration(new IntakePhotoConfiguration());
        builder.ApplyConfiguration(new CustomerProfileConfiguration());
        builder.ApplyConfiguration(new AccountSessionConfiguration());
        builder.ApplyConfiguration(new EmailVerificationChallengeConfiguration());
        builder.ApplyConfiguration(new PasswordResetChallengeConfiguration());
    }
}
