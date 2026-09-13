using Dealership.Application.Accounts;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.Identity;

// A pending invitation is not an account: login, recovery and handler lists never see it. Resending or editing
// the email renews the lifetime and replaces the nonce, which is what makes the previous link unusable.
public sealed class StaffInvitation : IMailDeliveryState
{
    public static StaffInvitation Create(ValidatedStaffInvitation invitation, Guid invitedById, DateTimeOffset now)
    {
        var created = new StaffInvitation
        {
            Id = invitation.Id,
            Email = invitation.Email,
            Role = invitation.Role,
            BranchId = invitation.BranchId,
            Name = invitation.Name,
            Mobile = invitation.Mobile,
            InvitedById = invitedById,
            CreatedAt = now
        };
        created.Renew(now);
        return created;
    }

    public Guid Id { get; init; }

    // Email, name and mobile are NULL together once the activated person is erased.
    public string? Email { get; set; }

    public string Role { get; init; } = string.Empty;

    public Guid? BranchId { get; init; }

    public string? Name { get; set; }

    public string? Mobile { get; set; }

    public Guid InvitedById { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid TokenNonce { get; set; }

    // Every send of this invitation, oldest first; resend limits read the recent ones and survive restarts.
    public DateTimeOffset[] SentAt { get; set; } = [];

    public DateTimeOffset? ActivatedAt { get; set; }

    public Guid? ActivatedUserId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? RevokedById { get; set; }

    public int DeliveryAttempts { get; set; }

    public DateTimeOffset NextDeliveryAt { get; set; }

    public Guid? DeliveryClaimId { get; set; }

    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    // Set once by the retention executor, which clears email, name and mobile of a terminal invitation.
    public DateTimeOffset? MinimizedAt { get; private set; }

    public bool IsPending(DateTimeOffset now) => ActivatedAt is null && RevokedAt is null && ExpiresAt > now &&
        Email is not null;

    // Queues a fresh send with a new link and the full lifetime.
    public void Renew(DateTimeOffset now)
    {
        ExpiresAt = now + StaffInvitationPolicy.Lifetime;
        TokenNonce = Guid.NewGuid();
        SentAt = [.. SentAt, now];
        DeliveryAttempts = 0;
        NextDeliveryAt = now;
        DeliveryClaimId = null;
        DeliveryClaimExpiresAt = null;
        DeliveredAt = null;
    }
}
