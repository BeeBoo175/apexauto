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

public sealed class CustomerSupportService(ICustomerSupportStore store, PasswordRecoveryService recovery,
    EmailVerificationService verification)
{
    public Task<NricRevealResult> RevealNricAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken) =>
        Valid(administratorId, customerId, request)
            ? store.RevealNricAsync(administratorId, customerId, Trim(request), cancellationToken)
            : Task.FromResult(new NricRevealResult(CustomerSupportStatus.Invalid));

    public Task<CustomerSupportStatus> RevokeSessionsAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken) =>
        Valid(administratorId, customerId, request)
            ? store.RevokeSessionsAsync(administratorId, customerId, Trim(request), cancellationToken)
            : Task.FromResult(CustomerSupportStatus.Invalid);

    // Sends the same reset link the customer would request themselves, within its own rate limits.
    public async Task<CustomerSupportStatus> SendResetAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken)
    {
        if (!Valid(administratorId, customerId, request))
        {
            return CustomerSupportStatus.Invalid;
        }

        var (status, contact) = await store.ReadContactAsync(administratorId, customerId, cancellationToken);
        if (contact is null || !contact.Active)
        {
            return contact is null ? status : CustomerSupportStatus.Conflict;
        }

        await recovery.RequestAsync(new PasswordRecoveryRequest(contact.Email), cancellationToken);
        return await store.RecordAsync(administratorId, customerId, AccountEventKind.ResetSent, Trim(request), cancellationToken);
    }

    // Sends the verification link again; the operation id doubles as the resend key, so a retry is safe.
    public async Task<CustomerSupportStatus> ResendVerificationAsync(Guid administratorId, Guid customerId,
        CustomerSupportRequest request, CancellationToken cancellationToken)
    {
        if (!Valid(administratorId, customerId, request))
        {
            return CustomerSupportStatus.Invalid;
        }

        var (status, contact) = await store.ReadContactAsync(administratorId, customerId, cancellationToken);
        if (contact is null || !contact.Active || contact.Verified)
        {
            return contact is null ? status : CustomerSupportStatus.Conflict;
        }

        var sent = await verification.ResendAsync(customerId, request.OperationId, cancellationToken);
        return sent.Status == VerificationStatus.Accepted
            ? await store.RecordAsync(administratorId, customerId, AccountEventKind.VerificationResent, Trim(request), cancellationToken)
            : CustomerSupportStatus.Conflict;
    }

    private static bool Valid(Guid administratorId, Guid customerId, CustomerSupportRequest request)
    {
        var reason = request.Reason?.Trim();
        return administratorId != Guid.Empty && customerId != Guid.Empty && request.OperationId != Guid.Empty &&
            !string.IsNullOrEmpty(reason) && reason.Length <= AccountEvent.ReasonMaximumLength &&
            !reason.Any(char.IsControl);
    }

    private static CustomerSupportRequest Trim(CustomerSupportRequest request) =>
        request with { Reason = request.Reason!.Trim() };
}
