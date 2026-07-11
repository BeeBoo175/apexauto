using Dealership.Domain.Accounts;

namespace Dealership.Application.Accounts;

// Every support action names the customer, carries the Administrator's reason and is recorded in AccountEvents.
public sealed record CustomerSupportRequest(Guid OperationId, string? Reason);

public enum CustomerSupportStatus
{
    Succeeded,
    Invalid,
    Forbidden,
    NotFound,
    // Already verified, suspended or deactivated, or the email could not be sent now.
    Conflict
}

public sealed record NricRevealResult(CustomerSupportStatus Status, string? Nric = null);

public sealed record CustomerContact(string Email, bool Verified, bool Active);

public interface ICustomerSupportStore
{
    // Reads the number and records the reveal in one transaction, so no reveal goes unlogged.
    Task<NricRevealResult> RevealNricAsync(Guid administratorId, Guid customerId, CustomerSupportRequest request,
        CancellationToken cancellationToken);

    Task<CustomerSupportStatus> RevokeSessionsAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken);

    Task<(CustomerSupportStatus Status, CustomerContact? Contact)> ReadContactAsync(Guid administratorId,
        Guid customerId, CancellationToken cancellationToken);

    Task<CustomerSupportStatus> RecordAsync(Guid administratorId, Guid customerId, AccountEventKind kind,
        CustomerSupportRequest request, CancellationToken cancellationToken);
}
