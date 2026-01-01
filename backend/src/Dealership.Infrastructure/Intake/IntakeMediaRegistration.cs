using Amazon.Runtime;
using Amazon.S3;
using Dealership.Application.Intake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Intake;

public static class IntakeMediaRegistration
{
    public static IServiceCollection AddIntakeMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PhotoStorageSettings>().Bind(configuration.GetRequiredSection("AWS"))
            .Validate(settings =>
            {
                settings.Validate();
                return true;
            }).ValidateOnStart();
        services.AddOptions<PhotoUploadSettings>().Bind(configuration.GetRequiredSection("IntakePhotos"))
            .Validate(settings => double.IsFinite(settings.UnsubmittedLifetimeHours) &&
                settings.UnsubmittedLifetimeHours is > 0 and <= 8760 &&
                TimeSpan.FromHours(settings.UnsubmittedLifetimeHours).Ticks >= 10,
                "IntakePhotos requires a positive unsubmitted lifetime of at most one year.").ValidateOnStart();
        services.AddOptions<PhotoUploadSettings>().Validate(settings =>
            double.IsFinite(settings.CancelledRetentionDays) && settings.CancelledRetentionDays is > 0 and <= 3650 &&
            TimeSpan.FromDays(settings.CancelledRetentionDays).Ticks >= 10,
            "IntakePhotos requires a positive cancelled-photo retention of at most ten years.").ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PhotoStorageSettings>>().Value);
        services.AddSingleton(provider =>
        {
            var lifetime = TimeSpan.FromHours(provider.GetRequiredService<IOptions<PhotoUploadSettings>>().Value.UnsubmittedLifetimeHours);
            return new IntakePhotoPolicy(TimeSpan.FromTicks(lifetime.Ticks - lifetime.Ticks % 10));
        });
        services.AddSingleton<IAmazonS3>(provider =>
        {
            var settings = provider.GetRequiredService<PhotoStorageSettings>();
            return new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), new AmazonS3Config
            {
                ServiceURL = settings.ServiceURL,
                AuthenticationRegion = settings.Region,
                ForcePathStyle = settings.ForcePathStyle,
                Timeout = TimeSpan.FromSeconds(30),
                MaxErrorRetry = 0,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
        });
        services.AddSingleton<IPhotoImageValidator, PhotoImageValidator>();
        services.AddSingleton<IPhotoStorage, S3PhotoStorage>();
        services.AddScoped<IntakePhotoService>();
        services.AddScoped<PhotoCleanupStore>();
        services.AddHostedService<PhotoCleanupWorker>();
        return services;
    }
}

public sealed class PhotoUploadSettings
{
    public double UnsubmittedLifetimeHours { get; set; }
    public double CancelledRetentionDays { get; set; }
    public bool CleanupEnabled { get; set; }
}

public sealed class PhotoStorageSettings
{
    public string ServiceURL { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool ForcePathStyle { get; set; }
    public bool AllowInsecureHttp { get; set; }

    public void Validate()
    {
        if (!Uri.TryCreate(ServiceURL, UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && !(endpoint.Scheme == Uri.UriSchemeHttp && (endpoint.IsLoopback || AllowInsecureHttp))) ||
            endpoint.UserInfo.Length > 0 || endpoint.AbsolutePath != "/" || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0 ||
            string.IsNullOrWhiteSpace(AccessKey) || string.IsNullOrWhiteSpace(SecretKey) || string.IsNullOrWhiteSpace(Region) ||
            BucketName.Length is < 3 or > 63 || BucketName.Any(character => character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')))
        {
            throw new InvalidOperationException("Private photo storage requires explicit credentials, region, bucket and a secure endpoint.");
        }
    }
}
