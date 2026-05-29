using Dealership.Domain.TestDrives;
using Dealership.Infrastructure.Mail;

namespace Dealership.Infrastructure.TestDrives;

public sealed class TestDriveMail : IMailDeliveryState
{
    private TestDriveMail()
    {
    }

    public TestDriveMail(TestDriveBooking booking, string recipient)
    {
        if (string.IsNullOrWhiteSpace(recipient) || recipient.Length > 256)
        {
            throw new ArgumentException("Test-drive confirmation requires a recipient.");
        }

        Id = Guid.NewGuid();
        BookingId = booking.Id;
        RecipientEmail = recipient;
        NextDeliveryAt = booking.CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid BookingId { get; private set; }
    public string RecipientEmail { get; private set; } = string.Empty;
    public int DeliveryAttempts { get; set; }
    public DateTimeOffset NextDeliveryAt { get; set; }
    public Guid? DeliveryClaimId { get; set; }
    public DateTimeOffset? DeliveryClaimExpiresAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
