namespace Dealership.Application.Accounts;

// The record kinds a customer closure can report; the service treats an unpaid provider bill as resolvable on its own.
public static class ClosureBlockerTypes
{
    public const string IntakeAppointment = "Inspection booking";
    public const string TestDrive = "Test drive";
    public const string DepositRequest = "Deposit request";
    public const string Reservation = "Reservation";
    public const string Sale = "Purchase";
    public const string DepositBill = "Deposit payment";
    public const string Refund = "Deposit refund";
}

public enum CustomerClosureStatus
{
    Closed,
    Invalid,
    // Employees and unknown accounts cannot close through the self-service command.
    Forbidden,
    WrongPassword,
    // A record still needs the customer; the result lists each one.
    Blocked,
    // A published sample account that the demo keeps available to every visitor.
    ProtectedSample,
    // The session was replaced or its credentials changed between the password check and the lock.
    SessionChanged,
    // The database rolled the attempt back for a lock conflict; nothing was changed.
    RetryLater
}

// A record that stops the customer closing the account, with the plain-words step that resolves it.
public sealed record ClosureBlocker(string Type, string Reference, string Status, string BranchName, string Resolution);

// One group of records that survives closure, naming what stays and when it ends.
public sealed record RetainedCategory(string Name, string Detail, string EndsWhen);

// The completed closure: access ended and every change made in one transaction. Picture and upload objects are removed
// afterwards from durable receipts, so the receipt says whether any removal is still waiting.
public sealed record CustomerClosureReceipt(Guid ReceiptId, DateTimeOffset ClosedAt, bool FileRemovalPending,
    IReadOnlyList<RetainedCategory> Retained);

public sealed record CustomerClosureResult(CustomerClosureStatus Status, IReadOnlyList<ClosureBlocker>? Blockers = null,
    CustomerClosureReceipt? Receipt = null);

public sealed record CustomerClosureRequest(Guid OperationId, string? Password);

public enum ClosurePasswordOutcome
{
    Verified,
    WrongPassword,
    NotCustomer,
    ProtectedSample
}

// The result of hashing the password before any transaction; the stamp lets the locked command detect a changed session.
public sealed record ClosurePasswordCheck(ClosurePasswordOutcome Outcome, string? SecurityStamp = null);

public interface ICustomerClosureStore
{
    // Null when the account is not an open customer.
    Task<IReadOnlyList<ClosureBlocker>?> BlockersAsync(Guid customerId, CancellationToken cancellationToken);

    Task<ClosurePasswordCheck> VerifyPasswordAsync(Guid customerId, string password, CancellationToken cancellationToken);

    // Requests whose unpaid provider bill still exists and must be deleted before the account can close.
    Task<IReadOnlyList<Guid>> UnreleasedBillRequestsAsync(Guid customerId, CancellationToken cancellationToken);

    // Rechecks every blocker under the closure locks and, when none remain, ends access and removes the customer's
    // personal data in one transaction. The operation id is the receipt, so a repeated command returns it unchanged.
    Task<CustomerClosureResult> CloseAsync(Guid customerId, Guid operationId, string verifiedSecurityStamp,
        CancellationToken cancellationToken);
}
