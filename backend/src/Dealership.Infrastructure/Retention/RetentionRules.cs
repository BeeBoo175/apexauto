namespace Dealership.Infrastructure.Retention;

internal enum RetentionHorizon
{
    SecurityArtifacts,
    MailDetails,
    Invitations,
    BusinessEvidence
}

// One routine minimization class: the rows whose terminal moment is at or before the cutoff ({0}) lose only the listed
// columns. Eligibility is repeated inside the UPDATE, so a row that changed meanwhile is simply left alone. The strings
// are fixed constants and never carry request data; the cutoff and cursor are always parameters. A business-evidence
// class receives the database clock itself as its cutoff and computes the deadline in SQL.
internal sealed record RetentionRule(string Name, string Table, RetentionHorizon Horizon, string Eligible, string Minimize,
    string Key = "Id");

internal static class RetentionRules
{
    private const string DeliveredMailEligible =
        "\"RecipientEmail\" IS NOT NULL AND \"DeliveredAt\" IS NOT NULL AND \"DeliveredAt\" <= {0}";

    private const string DeliveredMailMinimize = "\"RecipientEmail\" = NULL";

    private const string CheckInVehicle = "\"AppointmentCheckIns\".\"VehicleId\"";

    private const string OutcomeVehicle =
        "(SELECT b.\"VehicleId\" FROM \"TestDriveBookings\" b WHERE b.\"Id\" = \"TestDriveOutcomes\".\"BookingId\")";

    // LEAST ignores NULL, so the terminal moment of a link is its earliest expiry, consumption or invalidation.
    public static IReadOnlyList<RetentionRule> All { get; } = Array.AsReadOnly(
    [
        new RetentionRule("Sessions", "AccountSessions", RetentionHorizon.SecurityArtifacts,
            "\"MinimizedAt\" IS NULL AND LEAST(\"ExpiresAt\", \"RevokedAt\") <= {0}",
            "\"SecurityStamp\" = NULL, \"MinimizedAt\" = now()"),
        new RetentionRule("VerificationLinks", "EmailVerificationChallenges", RetentionHorizon.SecurityArtifacts,
            "\"RecipientEmail\" IS NOT NULL AND LEAST(\"ExpiresAt\", \"ConsumedAt\", \"InvalidatedAt\") <= {0}",
            "\"RecipientEmail\" = NULL"),
        new RetentionRule("ResetLinks", "PasswordResetChallenges", RetentionHorizon.SecurityArtifacts,
            "\"MinimizedAt\" IS NULL AND LEAST(\"ExpiresAt\", \"ConsumedAt\", \"InvalidatedAt\") <= {0}",
            "\"RecipientEmail\" = NULL, \"CompletedSecurityStamp\" = NULL, \"MinimizedAt\" = now()"),
        new RetentionRule("Invitations", "StaffInvitations", RetentionHorizon.Invitations,
            "\"MinimizedAt\" IS NULL AND \"Email\" IS NOT NULL AND " +
            "LEAST(\"ActivatedAt\", \"RevokedAt\", \"ExpiresAt\") <= {0}",
            "\"Email\" = NULL, \"Name\" = NULL, \"Mobile\" = NULL, \"MinimizedAt\" = now()"),
        new RetentionRule("AppointmentMail", "AppointmentMailOutbox", RetentionHorizon.MailDetails,
            DeliveredMailEligible, DeliveredMailMinimize),
        new RetentionRule("TestDriveMail", "TestDriveMailOutbox", RetentionHorizon.MailDetails,
            DeliveredMailEligible, DeliveredMailMinimize),
        new RetentionRule("SaleDeliveryMail", "SaleDeliveryMailOutbox", RetentionHorizon.MailDetails,
            DeliveredMailEligible, DeliveredMailMinimize),
        new RetentionRule("DepositMail", "ReservationDepositMailOutbox", RetentionHorizon.MailDetails,
            DeliveredMailEligible, DeliveredMailMinimize),
        new RetentionRule("CheckInNotes", "AppointmentCheckIns", RetentionHorizon.BusinessEvidence,
            "\"Notes\" IS NOT NULL AND " +
            Deadline("GREATEST(\"CheckedInAt\", \"CorrectionRecordedAt\", " + VehicleSaleAnchor(CheckInVehicle) + ")") +
            " AND " + NoUnfinishedSale(CheckInVehicle),
            "\"Notes\" = NULL", "AppointmentId"),
        new RetentionRule("TestDriveOutcomeNotes", "TestDriveOutcomes", RetentionHorizon.BusinessEvidence,
            "\"Notes\" IS NOT NULL AND " +
            Deadline("GREATEST(\"EndedAt\", " + VehicleSaleAnchor(OutcomeVehicle) + ")") +
            " AND " + NoUnfinishedSale(OutcomeVehicle),
            "\"Notes\" = NULL"),
        // A reservation keeps its buyer name; the contact and NRIC copies go once its sale is finished (or it was
        // released), no refund of the customer is still owed and the deadline of the whole chain has passed.
        new RetentionRule("ReservationBuyerCopies", "Reservations", RetentionHorizon.BusinessEvidence,
            "NOT \"IsActive\" AND (\"BuyerEmail\" IS NOT NULL OR \"BuyerMobile\" IS NOT NULL OR \"BuyerNric\" IS NOT NULL) AND " +
            "NOT EXISTS (SELECT 1 FROM \"SalesOrders\" s WHERE s.\"Id\" = \"Reservations\".\"Id\" AND " +
            "s.\"Status\" NOT IN ('Delivered', 'Cancelled', 'Expired')) AND " +
            "NOT EXISTS (SELECT 1 FROM \"ReservationRefunds\" f WHERE f.\"CustomerId\" = \"Reservations\".\"CustomerId\" AND " +
            "f.\"State\" <> 'Completed') AND " +
            Deadline("GREATEST(\"ExpiresAt\", " +
                "(SELECT max(r.\"RecordedAt\") FROM \"ReservationReleases\" r WHERE r.\"ReservationId\" = \"Reservations\".\"Id\"), " +
                "(SELECT max(e.\"RecordedAt\") FROM \"SalesOrderEvents\" e WHERE e.\"SalesOrderId\" = \"Reservations\".\"Id\"))"),
            "\"BuyerEmail\" = NULL, \"BuyerMobile\" = NULL, \"BuyerNric\" = NULL"),
        // Bank code and last four digits stay as reconciliation evidence; the holder name goes after completion.
        new RetentionRule("RefundHolder", "ReservationRefunds", RetentionHorizon.BusinessEvidence,
            "\"AccountHolder\" IS NOT NULL AND \"State\" = 'Completed' AND " + Deadline("\"SettledAt\""),
            "\"AccountHolder\" = NULL", "PaymentRequestId")
    ]);

    private static string Deadline(string anchor) => $"public.retention_business_deadline({anchor}) <= {{0}}";

    // The latest sale event of the vehicle: a later sale on the same vehicle extends the chain's anchor.
    private static string VehicleSaleAnchor(string vehicle) =>
        "(SELECT max(e.\"RecordedAt\") FROM \"SalesOrderEvents\" e JOIN \"SalesOrders\" s ON s.\"Id\" = e.\"SalesOrderId\" " +
        $"WHERE s.\"VehicleId\" = {vehicle})";

    // A vehicle with a sale that is not yet Delivered, Cancelled or Expired keeps every record of its chain.
    private static string NoUnfinishedSale(string vehicle) =>
        $"NOT EXISTS (SELECT 1 FROM \"SalesOrders\" s WHERE s.\"VehicleId\" = {vehicle} AND " +
        "s.\"Status\" NOT IN ('Delivered', 'Cancelled', 'Expired'))";
}
