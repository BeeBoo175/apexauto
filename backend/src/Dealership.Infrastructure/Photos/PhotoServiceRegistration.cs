using Dealership.Application.Accounts;
using Dealership.Application.Photos;
using Dealership.Application.Intake;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Accounts;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Vehicles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Photos;

public static class PhotoServiceRegistration
{
    public static IServiceCollection AddPhotos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IntakePhotoSettings>().Bind(configuration.GetRequiredSection("IntakePhotos"))
            .Validate(settings => double.IsFinite(settings.UnsubmittedLifetimeHours) &&
                settings.UnsubmittedLifetimeHours is > 0 and <= 8760 &&
                TimeSpan.FromHours(settings.UnsubmittedLifetimeHours).Ticks >= 10,
                "IntakePhotos requires a positive unsubmitted lifetime of at most one year.").ValidateOnStart();
        services.AddOptions<IntakePhotoSettings>().Validate(settings =>
            double.IsFinite(settings.CancelledRetentionDays) && settings.CancelledRetentionDays is > 0 and <= 3650 &&
            TimeSpan.FromDays(settings.CancelledRetentionDays).Ticks >= 10,
            "IntakePhotos requires a positive cancelled-photo retention of at most ten years.").ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var lifetime = TimeSpan.FromHours(provider.GetRequiredService<IOptions<IntakePhotoSettings>>().Value.UnsubmittedLifetimeHours);
            return new IntakePhotoPolicy(TimeSpan.FromTicks(lifetime.Ticks - lifetime.Ticks % 10));
        });
        services.AddOptions<PhotoCleanupSettings>().Bind(configuration.GetSection("PhotoCleanup"))
            .Validate(settings => double.IsFinite(settings.PendingUploadLifetimeHours) &&
                settings.PendingUploadLifetimeHours is > 0 and <= 8760 &&
                TimeSpan.FromHours(settings.PendingUploadLifetimeHours).Ticks >= 10,
                "PhotoCleanup requires a positive pending upload lifetime of at most one year.")
            .Validate(settings => settings.BatchSize is >= 1 and <= 100,
                "PhotoCleanup requires a batch size between 1 and 100.").ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var lifetime = TimeSpan.FromHours(provider.GetRequiredService<IOptions<PhotoCleanupSettings>>().Value.PendingUploadLifetimeHours);
            return new AccountPicturePolicy(TimeSpan.FromTicks(lifetime.Ticks - lifetime.Ticks % 10));
        });
        services.AddSingleton<IPhotoImageValidator, PhotoImageValidator>();
        services.AddSingleton(provider => new InspectionPhotoPolicy(
            provider.GetRequiredService<IntakePhotoPolicy>().UnsubmittedLifetime));
        services.AddSingleton<IPhotoStorage, S3PhotoStorage>();
        services.AddSingleton<IPhotoThumbnailer, PhotoThumbnailer>();
        services.AddScoped<PhotoCleanupStore>();
        services.AddScoped<IPhotoCleanupSource>(provider => provider.GetRequiredService<PhotoCleanupStore>());
        services.AddHostedService<PhotoCleanupWorker>();
        services.AddScoped<InspectionPhotoCleanupStore>();
        services.AddScoped<IPhotoCleanupSource>(provider => provider.GetRequiredService<InspectionPhotoCleanupStore>());
        services.AddScoped<AccountPictureCleanupStore>();
        services.AddScoped<IPhotoCleanupSource>(provider => provider.GetRequiredService<AccountPictureCleanupStore>());
        services.AddScoped<ListingPhotoCleanupStore>();
        services.AddScoped<IPhotoCleanupSource>(provider => provider.GetRequiredService<ListingPhotoCleanupStore>());
        return services;
    }
}
