using Dealership.Infrastructure.Configuration;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Branches;

namespace Dealership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is required in every environment.");
        }

        services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            // Email is the login identifier; a separate username alphabet must not narrow valid emails.
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<DealershipDbContext>()
            .AddPasswordValidator<AccountPasswordValidator>();
        services.AddScoped<ICustomerRegistrationStore, CustomerRegistrationStore>();
        services.AddScoped<IAccountSessionStore, AccountSessionStore>();
        services.AddScoped<ICustomerAccountQuery, CustomerAccountQuery>();
        services.AddScoped<IStaffProvisioningStore, StaffProvisioningStore>();
        services.AddScoped<IBranchProvisioningStore, BranchProvisioningStore>();
        services.AddScoped<IEmailVerificationStore, EmailVerificationStore>();
        services.AddSingleton<EmailVerificationToken>();
        services.AddScoped<IPasswordRecoveryStore, PasswordRecoveryStore>();
        services.AddSingleton<PasswordResetToken>();
        services.AddScoped<IPublicBranchStore, PublicBranchStore>();
        services.AddScoped<IIntakePhotoStore, IntakePhotoStore>();
        services.AddScoped<IPhotoTransferLock, PhotoTransferLock>();
        services.AddScoped<IIntakeBookingStore, IntakeBookingStore>();
        services.AddScoped<IAppointmentHistoryStore, AppointmentHistoryStore>();
        services.AddScoped<IAppointmentCancellationStore, AppointmentCancellationStore>();
        services.AddSingleton<MissingAccountPasswordHash>(provider =>
        {
            using var scope = provider.CreateScope();
            return new MissingAccountPasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountUser>>());
        });

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
