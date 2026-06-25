using Dealership.Application.Accounts;

namespace Dealership.UnitTests;

public sealed class RegistrationServiceTests
{
    [Theory]
    [InlineData("nric")]
    [InlineData("email")]
    [InlineData("legalName")]
    [InlineData("registrationKey")]
    [InlineData("username")]
    public async Task InvalidProfileReturnsItsFieldErrorBeforeCallingPersistence(string field)
    {
        var request = new RegistrationRequest(Guid.NewGuid(), "Farid Hassan", "farid.hassan@example.invalid",
            "Password123!", "900101101234", "0123456789", "24 Jalan Kenanga, Shah Alam, Selangor", "farid.hassan");
        request = field switch
        {
            "nric" => request with { Nric = "９００１０１０１０１２３" },
            "email" => request with { Email = "invalid" },
            "legalName" => request with { LegalName = "  " },
            "registrationKey" => request with { RegistrationKey = Guid.Empty },
            "username" => request with { Username = "a b" },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        var service = new RegistrationService(new UnexpectedRegistrationStore());
        var result = await service.RegisterAsync(request, CancellationToken.None);

        Assert.Equal(RegistrationStatus.Invalid, result.Status);
        Assert.Single(result.Errors);
        Assert.Contains(field, result.Errors.Keys);
    }

    private sealed class UnexpectedRegistrationStore : ICustomerRegistrationStore
    {
        public Task<RegistrationResult> RegisterAsync(ValidatedRegistration registration, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Invalid registration must not reach persistence.");
        }
    }
}
