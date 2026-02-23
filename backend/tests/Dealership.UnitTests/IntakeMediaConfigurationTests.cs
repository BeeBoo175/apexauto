using Dealership.Infrastructure.Photos;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.UnitTests;

public sealed class IntakeMediaConfigurationTests
{
    [Theory]
    [InlineData("IntakePhotos:UnsubmittedLifetimeHours", "0")]
    [InlineData("IntakePhotos:UnsubmittedLifetimeHours", "8761")]
    [InlineData("IntakePhotos:UnsubmittedLifetimeHours", "NaN")]
    [InlineData("IntakePhotos:UnsubmittedLifetimeHours", "0.000000000001")]
    [InlineData("IntakePhotos:CancelledRetentionDays", "0")]
    [InlineData("IntakePhotos:CancelledRetentionDays", "3651")]
    [InlineData("IntakePhotos:CancelledRetentionDays", "Infinity")]
    [InlineData("IntakePhotos:CancelledRetentionDays", "0.000000000001")]
    public void InvalidLifecycleConfigurationFailsAtStartup(string key, string value)
    {
        var configuration = Configuration(key, value);
        var services = new ServiceCollection();
        services.AddPhotos(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void InvalidStorageConfigurationFailsAtStartupWithoutIntakeRegistration()
    {
        var services = new ServiceCollection();
        services.AddS3Storage(Configuration("AWS:SecretKey", string.Empty));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void IntakeLifecycleConfigurationRetainsUpperBoundsAndCleanupSwitchWithoutStorage()
    {
        var services = new ServiceCollection();
        services.AddPhotos(Configuration("IntakePhotos:CleanupEnabled", "true"));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();

        var settings = provider.GetRequiredService<IOptions<IntakePhotoSettings>>().Value;
        Assert.Equal(8760, settings.UnsubmittedLifetimeHours);
        Assert.Equal(3650, settings.CancelledRetentionDays);
        Assert.True(settings.CleanupEnabled);
        Assert.Equal(TimeSpan.FromHours(8760), provider.GetRequiredService<IntakePhotoPolicy>().UnsubmittedLifetime);
    }

    private static IConfiguration Configuration(string key, string value)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AWS:ServiceURL"] = "https://storage.example.invalid",
            ["AWS:AccessKey"] = "test-access",
            ["AWS:SecretKey"] = "test-secret",
            ["AWS:BucketName"] = "apexauto-photos",
            ["AWS:Region"] = "auto",
            ["IntakePhotos:UnsubmittedLifetimeHours"] = "8760",
            ["IntakePhotos:CancelledRetentionDays"] = "3650",
            ["IntakePhotos:CleanupEnabled"] = "false"
        };
        settings[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
