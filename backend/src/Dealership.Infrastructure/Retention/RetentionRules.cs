namespace Dealership.Infrastructure.Retention;

internal enum RetentionHorizon
{
    SecurityArtifacts,
    MailDetails,
    Invitations
}

// One routine minimization class: the rows whose terminal moment is at or before the cutoff ({0}) lose only the listed
// columns. Eligibility is repeated inside the UPDATE, so a row that changed meanwhile is simply left alone. The strings
// are fixed constants and never carry request data; the cutoff and cursor are always parameters.
internal sealed record RetentionRule(string Name, string Table, RetentionHorizon Horizon, string Eligible, string Minimize);

internal static class RetentionRules
{
    private const string DeliveredMailEligible =
        "\"RecipientEmail\" IS NOT NULL AND \"DeliveredAt\" IS NOT NULL AND \"DeliveredAt\" <= {0}";

    private const string DeliveredMailMinimize = "\"RecipientEmail\" = NULL";

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
            DeliveredMailEligible, DeliveredMailMinimize)
    ]);
}
