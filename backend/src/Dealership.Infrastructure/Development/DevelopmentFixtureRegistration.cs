using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Infrastructure.Development;

public static class DevelopmentFixtureRegistration
{
    public static IServiceCollection AddDevelopmentAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(AppointmentScheduleConfiguration.Read(configuration));
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
        services.AddScoped<DevelopmentTestDriveFixtures>();
        services.AddScoped<DevelopmentTestDriveVolumeFixtures>();
        services.AddScoped<DevelopmentTestDriveWithdrawalFixtures>();
        services.AddScoped<DevelopmentTestDriveReopeningFixtures>();
        services.AddScoped<DevelopmentTestDriveRepairFixtures>();
        services.AddScoped<DevelopmentTestDriveHandlingFixtures>();
        services.AddScoped<DevelopmentSaleFixtures>();
        services.AddScoped<DevelopmentSaleVolumeFixtures>();
        services.AddScoped<DevelopmentInspectionScenarioFixtures>();
        services.AddScoped<DevelopmentIntakeFixtures>();
        services.AddScoped<DevelopmentIntakeOwnershipFixtures>();
        services.AddScoped<DevelopmentWorkshopHistory>();
        services.AddScoped<DevelopmentListingHistory>();
        services.AddScoped<DevelopmentReservationHistory>();
        services.AddScoped<DevelopmentSaleHistory>();
        services.AddScoped<DevelopmentTestDriveHistory>();
        services.AddScoped<DevelopmentFixturePhotos>();
        return services;
    }
}
