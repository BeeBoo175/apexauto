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
// class receives the database clock itself as its cutoff and computes the deadline in SQL. A rule with a Statement runs
// that whole statement instead of the UPDATE ({0} the cutoff, {1} the key): it queues an exact-key photograph removal.
internal sealed record RetentionRule(string Name, string Table, RetentionHorizon Horizon, string Eligible, string Minimize,
    string Key = "Id", string? Statement = null);

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
        // released), no money of the customer is still owed, no hold applies and the chain's deadline has passed.
        new RetentionRule("ReservationBuyerCopies", "Reservations", RetentionHorizon.BusinessEvidence,
            "NOT \"IsActive\" AND (\"BuyerEmail\" IS NOT NULL OR \"BuyerMobile\" IS NOT NULL OR \"BuyerNric\" IS NOT NULL) AND " +
            "NOT EXISTS (SELECT 1 FROM \"SalesOrders\" s WHERE s.\"Id\" = \"Reservations\".\"Id\" AND " +
            "(s.\"Status\" NOT IN ('Delivered', 'Cancelled', 'Expired') OR s.\"RetentionHoldUntil\" > {0})) AND " +
            "NOT " + MoneyOwed("\"Reservations\".\"CustomerId\"") + " AND " +
            Deadline("GREATEST(\"ExpiresAt\", " +
                "(SELECT max(r.\"RecordedAt\") FROM \"ReservationReleases\" r WHERE r.\"ReservationId\" = \"Reservations\".\"Id\"), " +
                "(SELECT max(e.\"RecordedAt\") FROM \"SalesOrderEvents\" e WHERE e.\"SalesOrderId\" = \"Reservations\".\"Id\"))"),
            "\"BuyerEmail\" = NULL, \"BuyerMobile\" = NULL, \"BuyerNric\" = NULL"),
        // Bank code and last four digits stay as reconciliation evidence; the holder name goes after completion.
        new RetentionRule("RefundHolder", "ReservationRefunds", RetentionHorizon.BusinessEvidence,
            "\"AccountHolder\" IS NOT NULL AND \"State\" = 'Completed' AND " + Deadline("\"SettledAt\""),
            "\"AccountHolder\" = NULL", "PaymentRequestId"),
        // The buyer's name, NRIC and address leave the settlement review, both issued documents and corrections of a
        // delivered sale; vehicle facts, terms and payment evidence stay. A Settled sale never qualifies.
        new RetentionRule("SaleReviews", "SalesOrderEvents", RetentionHorizon.BusinessEvidence,
            "\"SettlementReview\"->'BuyerName' IS NOT NULL AND " + FinishedSale("\"SalesOrderEvents\".\"SalesOrderId\""),
            "\"SettlementReview\" = \"SettlementReview\" - 'BuyerName' - 'BuyerNric' - 'BuyerAddress'"),
        new RetentionRule("SaleDocuments", "SaleDocuments", RetentionHorizon.BusinessEvidence,
            "\"ContentsJson\"->'Review'->'BuyerName' IS NOT NULL AND " + FinishedSale("\"SaleDocuments\".\"SalesOrderId\""),
            "\"ContentsJson\" = jsonb_set(\"ContentsJson\", ARRAY['Review'], " +
            "(\"ContentsJson\"->'Review') - 'BuyerName' - 'BuyerNric' - 'BuyerAddress')"),
        new RetentionRule("SaleCorrections", "SaleDocumentCorrections", RetentionHorizon.BusinessEvidence,
            "\"MinimizedAt\" IS NULL AND " + FinishedSale("\"SaleDocumentCorrections\".\"SalesOrderId\""),
            "\"BuyerName\" = NULL, \"BuyerAddress\" = NULL, \"Reason\" = NULL, \"MinimizedAt\" = now()"),
        // The signed report keeps its findings, vehicle facts and photograph hashes; the technician's sign-in name
        // leaves it once the inspection's chain is old enough.
        new RetentionRule("SignedTechnician", "InspectionSignedRevisions", RetentionHorizon.BusinessEvidence,
            "\"ReportJson\"->'technicianIdentity' IS NOT NULL AND " +
            InspectionDue("\"InspectionSignedRevisions\".\"InspectionId\""),
            "\"ReportJson\" = \"ReportJson\" - 'technicianIdentity'"),
        // Photographs of a finished chain are queued for the existing exact-key removal; the worker deletes the object.
        PhotoRemoval("InspectionPhotos", "InspectionPhotos", "InspectionPhotoRemovals",
            "EXISTS (SELECT 1 FROM \"InspectionDraftPhotos\" l WHERE l.\"PhotoId\" = \"InspectionPhotos\".\"Id\") AND " +
            "NOT EXISTS (SELECT 1 FROM \"InspectionPhotoRemovals\" x WHERE x.\"PhotoId\" = \"InspectionPhotos\".\"Id\") AND " +
            InspectionDue("\"InspectionPhotos\".\"InspectionId\"")),
        PhotoRemoval("CheckInPhotos", "IntakePhotos", "PhotoRemovals",
            "EXISTS (SELECT 1 FROM \"AppointmentPhotos\" a JOIN \"AppointmentCheckIns\" k " +
            "ON k.\"AppointmentId\" = a.\"AppointmentId\" WHERE a.\"PhotoId\" = \"IntakePhotos\".\"Id\" AND " +
            Deadline("GREATEST(k.\"CheckedInAt\", k.\"CorrectionRecordedAt\", " + VehicleSaleAnchor("k.\"VehicleId\"") + ")") +
            " AND " + NoUnfinishedSale("k.\"VehicleId\"") + ") AND " +
            "NOT EXISTS (SELECT 1 FROM \"PhotoRemovals\" x WHERE x.\"PhotoId\" = \"IntakePhotos\".\"Id\")")
    ]);

    // Exact-key photograph removal: the receipt is inserted once and the shared photo worker deletes the object.
    private static RetentionRule PhotoRemoval(string name, string table, string removals, string eligible) =>
        new(name, table, RetentionHorizon.BusinessEvidence, eligible, string.Empty, Statement:
            $"INSERT INTO \"{removals}\" (\"PhotoId\", \"Reason\", \"EligibleAt\", \"RequestedAt\", \"Attempts\", \"NextAttemptAt\") " +
            $"SELECT \"Id\", 'RetentionExpired', {{0}}, now(), 0, now() FROM \"{table}\" " +
            $"WHERE \"Id\" = {{1}} AND ({eligible})");

    // A signed inspection is due once the latest signature of the inspection and the latest sale event of its
    // vehicle lie beyond the years in force, and no sale of the vehicle is unfinished or held.
    private static string InspectionDue(string inspectionId)
    {
        var vehicle = $"(SELECT d.\"VehicleId\" FROM \"InspectionDrafts\" d WHERE d.\"Id\" = {inspectionId})";
        return Deadline("GREATEST((SELECT max(r.\"SignedAt\") FROM \"InspectionSignedRevisions\" r " +
            $"WHERE r.\"InspectionId\" = {inspectionId}), " + VehicleSaleAnchor(vehicle) + ")") +
            " AND " + NoUnfinishedSale(vehicle);
    }

    private static string Deadline(string anchor) => $"public.retention_business_deadline({anchor}) <= {{0}}";

    // Money of the customer is owed while a refund is not completed, or a deposit was paid unmatched or after a
    // release and no completed refund returned it.
    private static string MoneyOwed(string customer) =>
        $"(EXISTS (SELECT 1 FROM \"ReservationRefunds\" f WHERE f.\"CustomerId\" = {customer} AND f.\"State\" <> 'Completed') OR " +
        "EXISTS (SELECT 1 FROM \"ReservationDepositPayments\" p JOIN \"ReservationDepositRequests\" q ON q.\"Id\" = p.\"RequestId\" " +
        $"WHERE q.\"CustomerId\" = {customer} AND (p.\"State\" = 'PaidUnmatched' AND p.\"PaidAmountSen\" > 0 OR " +
        "p.\"State\" = 'Paid' AND EXISTS (SELECT 1 FROM \"ReservationReleases\" r WHERE r.\"ReservationId\" = p.\"RequestId\")) AND " +
        "NOT EXISTS (SELECT 1 FROM \"ReservationRefunds\" f WHERE f.\"PaymentRequestId\" = p.\"RequestId\" AND f.\"State\" = 'Completed')))";

    // A delivered sale without a live hold or money owed whose latest anchor (delivery, any history entry, correction
    // or refund of its deposit) lies beyond the years in force.
    private static string FinishedSale(string saleId) =>
        $"EXISTS (SELECT 1 FROM \"SalesOrders\" s WHERE s.\"Id\" = {saleId} AND s.\"Status\" = 'Delivered' AND " +
        "(s.\"RetentionHoldUntil\" IS NULL OR s.\"RetentionHoldUntil\" <= {0}) AND " +
        "NOT " + MoneyOwed("s.\"CustomerId\"") + " AND " +
        Deadline("GREATEST(s.\"DeliveredAt\", " +
            "(SELECT max(e.\"RecordedAt\") FROM \"SalesOrderEvents\" e WHERE e.\"SalesOrderId\" = s.\"Id\"), " +
            "(SELECT max(c.\"RecordedAt\") FROM \"SaleDocumentCorrections\" c WHERE c.\"SalesOrderId\" = s.\"Id\"), " +
            "(SELECT max(f.\"SettledAt\") FROM \"ReservationRefunds\" f WHERE f.\"PaymentRequestId\" = s.\"Id\"))") + ")";

    // The latest sale event of the vehicle: a later sale on the same vehicle extends the chain's anchor.
    private static string VehicleSaleAnchor(string vehicle) =>
        "(SELECT max(e.\"RecordedAt\") FROM \"SalesOrderEvents\" e JOIN \"SalesOrders\" s ON s.\"Id\" = e.\"SalesOrderId\" " +
        $"WHERE s.\"VehicleId\" = {vehicle})";

    // A vehicle with a sale that is not yet Delivered, Cancelled or Expired, or whose sale is under a live hold,
    // keeps every record of its chain.
    private static string NoUnfinishedSale(string vehicle) =>
        $"NOT EXISTS (SELECT 1 FROM \"SalesOrders\" s WHERE s.\"VehicleId\" = {vehicle} AND " +
        "(s.\"Status\" NOT IN ('Delivered', 'Cancelled', 'Expired') OR s.\"RetentionHoldUntil\" > {0}))";
}
