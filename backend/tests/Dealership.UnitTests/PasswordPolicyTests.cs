using Dealership.Infrastructure;
using Dealership.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.UnitTests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Aa1!aaa", "PasswordTooShort")]
    [InlineData("password123!", "PasswordRequiresUpper")]
    [InlineData("PASSWORD123!", "PasswordRequiresLower")]
    [InlineData("Password!!!", "PasswordRequiresDigit")]
    [InlineData("Password123", "PasswordRequiresNonAlphanumeric")]
    [InlineData("Password123 ", "PasswordRequiresSymbol")]
    public async Task ConfiguredProductionValidatorsRejectEachPasswordRule(string password, string expectedError)
    {
        // No host is configured: password validation must never open a database connection.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Pooling=false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = new AccountUser { Id = Guid.NewGuid() };
        var errors = new List<IdentityError>();

        foreach (var validator in manager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(manager, user, password);
            errors.AddRange(result.Errors);
        }

        Assert.Contains(errors, error => error.Code == expectedError);
    }
}
