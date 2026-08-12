using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Identity;

public sealed class StaffInvitationMailSource(DealershipDbContext database, AuthMailLinks links,
    StaffInvitationToken tokens, TimeProvider clock) : IMailDeliverySource
{
    public Task<ClaimedMail?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var candidates = database.StaffInvitations.FromSqlInterpolated($"""
            SELECT * FROM "StaffInvitations"
            WHERE "DeliveredAt" IS NULL AND "ActivatedAt" IS NULL AND "RevokedAt" IS NULL
              AND "ExpiresAt" > {now} AND "NextDeliveryAt" <= {now}
              AND ("DeliveryClaimExpiresAt" IS NULL OR "DeliveryClaimExpiresAt" <= {now})
            ORDER BY "NextDeliveryAt", "CreatedAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """);

        return MailDeliveryBookkeeping.ClaimNextAsync(database, candidates, now,
            (invitation, _) => Task.FromResult(Compose(invitation)), cancellationToken);
    }

    public Task MarkDeliveredAsync(Guid messageId, Guid claimId, CancellationToken cancellationToken)
    {
        return MailDeliveryBookkeeping.MarkDeliveredAsync(database.StaffInvitations, messageId, claimId,
            clock.GetUtcNow(), cancellationToken);
    }

    private OutboundMail Compose(StaffInvitation invitation)
    {
        var link = links.Create("/activate-account", tokens.Create(invitation));
        var body = $"{invitation.Name}, you have been invited to ApexAuto. Open this link to choose your password " +
            $"and username:\n\n{link}\n\nThis link expires in {StaffInvitationPolicy.LifetimeHours} hours and works once. " +
            "If you were not expecting this invitation, ignore this message.";
        var html = MailTemplate.Render(new MailLayout(
            "You are invited to ApexAuto",
            [$"{invitation.Name}, you have been invited to join the ApexAuto staff workspace. Choose your password and username to activate your account."],
            [],
            [$"This link expires in {StaffInvitationPolicy.LifetimeHours} hours and works once. If you were not expecting this invitation, ignore this message."],
            "Activate account",
            link));
        return new OutboundMail(invitation.Email, "You are invited to ApexAuto", body, html);
    }
}
