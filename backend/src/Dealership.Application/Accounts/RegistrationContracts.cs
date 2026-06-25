namespace Dealership.Application.Accounts;

public sealed record RegistrationRequest(Guid RegistrationKey, string? LegalName, string? Email,
    string? Password, string? Nric, string? Mobile, string? ResidentialAddress, string? Username);

public enum RegistrationStatus
{
    Succeeded,
    Invalid,
    Conflict
}

public sealed record RegistrationResult(RegistrationStatus Status, IReadOnlyDictionary<string, string[]> Errors);

public sealed record ValidatedRegistration(Guid RegistrationKey, string LegalName, string Email,
    string Password, string Nric, string Mobile, string ResidentialAddress, string Username);

public interface ICustomerRegistrationStore
{
    Task<RegistrationResult> RegisterAsync(ValidatedRegistration registration, CancellationToken cancellationToken);
}
