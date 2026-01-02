using Dealership.Application.Vehicles;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ValuationService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<StaffProvisioningService>();
        services.AddScoped<BranchProvisioningService>();
        services.AddScoped<BranchDiscoveryService>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<PasswordRecoveryService>();

        return services;
    }
}
