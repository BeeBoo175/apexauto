using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentFixtureRegistration
{
    public static IServiceCollection AddDevelopmentAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(InspectionScheduleConfiguration.Read(configuration));
        services.AddSingleton<DevelopmentFixtureClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<DevelopmentFixtureClock>());
        services.AddScoped<DevelopmentAccountFixtures>();
        services.AddScoped<DevelopmentCustomerFixtures>();
        return services;
    }

    public static IServiceCollection AddDevelopmentFixtures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDevelopmentAccounts(configuration);
        services.AddScoped<DevelopmentFixtureRunner>();
        services.AddScoped<DevelopmentFixtureMail>();
        services.AddScoped<DevelopmentInspectionFixtures>();
        services.AddScoped<DevelopmentListingFixtures>();
        services.AddScoped<DevelopmentPurchaseFixtures>();
        services.AddScoped<DevelopmentReservationFixtures>();
        services.AddScoped<DevelopmentSaleFixtures>();
        services.AddScoped<DevelopmentInspectionScenarioFixtures>();
        services.AddScoped<DevelopmentIntakeFixtures>();
        services.AddScoped<DevelopmentIntakeOwnershipFixtures>();
        services.AddScoped<DevelopmentFixturePhotos>();
        return services;
    }
}
