using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Storage;

public static class S3StorageRegistration
{
    public static IServiceCollection AddS3Storage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PhotoStorageSettings>().Bind(configuration.GetRequiredSection("AWS"))
            .Validate(settings =>
            {
                settings.Validate();
                return true;
            }).ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<PhotoStorageSettings>>().Value);
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
        return services;
    }
}
