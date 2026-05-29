using System.Text.Json;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal static class DevelopmentTestDriveFixtureAssertions
{
    internal static async Task<string> SnapshotAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var bookings = await database.TestDriveBookings.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var mail = await database.TestDriveMailOutbox.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        var cancellations = await database.TestDriveCancellations.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync();
        Assert.Equal(DevelopmentTestDriveFixtures.Scenarios.Count, bookings.Length);
        Assert.Single(cancellations);
        Assert.Equal(bookings.Length + cancellations.Length, mail.Length);
        Assert.Equal(bookings.Length, bookings.Select(item => item.CustomerId).Distinct().Count());
        Assert.All(mail, message =>
        {
            Assert.Contains(bookings, booking => booking.Id == message.BookingId);
            Assert.NotNull(message.DeliveredAt);
            Assert.Equal(0, message.DeliveryAttempts);
        });
        return JsonSerializer.Serialize(new { Bookings = bookings, Cancellations = cancellations, Mail = mail });
    }
}
