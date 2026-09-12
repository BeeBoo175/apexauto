namespace Dealership.Application.Accounts;

// What an erased customer's records keep, in the owner's words: the closure result is pseudonymized, never anonymous.
public static class ClosureRetention
{
    private const string SevenYears = "Up to seven years after the related sale, hold or refund ends";

    public static IReadOnlyList<RetainedCategory> Categories { get; } = Array.AsReadOnly<RetainedCategory>(
    [
        new("Issued sale documents", "Your name, NRIC and residential address stay on purchase orders, receipts and " +
            "handover notes, and in the settlement record they came from.", SevenYears),
        new("Reservation records", "Your name stays on reservation confirmations with the reservation number, prices " +
            "and dates. Email, mobile and NRIC copies are removed unless a settlement document needs the NRIC.", SevenYears),
        new("Deposit and refund records", "Amounts, references and dates stay, together with what the payment provider " +
            "holds: the payer name, email and account holder.", SevenYears),
        new("Vehicle history", "The plate, VIN and history of any car you sold or booked stay with that vehicle, " +
            "because a vehicle record cannot disappear with its former owner.", "No end date"),
        new("Inspection evidence", "Signed inspection reports and retained photographs stay as evidence, including the " +
            "technician details and any metadata inside the photographs.", SevenYears),
        new("Account history", "A record that the account was closed, with its time, stays. Earlier reasons and " +
            "changed values in your account history are removed.", "No end date"),
        new("Earlier copies", "Emails already delivered stay in the inbox they reached, and backups made before today " +
            "can contain your details until they are discarded.", "Outside this application")
    ]);
}
