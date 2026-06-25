using Dealership.Application.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

public sealed class RecordHandlerQueryTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task OnlyTheCurrentAppointmentHandlerHandlesTheCustomer()
    {
        using var data = await CheckInTestData.CreateAsync(fixture);
        var appointment = await data.BookAsync(dateOffset: 1);
        using var scope = data.Factory.Services.CreateScope();
        var handlers = scope.ServiceProvider.GetRequiredService<IRecordHandlerQuery>();

        Assert.False(await handlers.HandlesCustomerAsync(data.StaffId, data.OwnerId, CancellationToken.None));

        await data.ClaimAsync(appointment.Id);

        Assert.True(await handlers.HandlesCustomerAsync(data.StaffId, data.OwnerId, CancellationToken.None));
        Assert.False(await handlers.HandlesCustomerAsync(Guid.NewGuid(), data.OwnerId, CancellationToken.None));
        Assert.False(await handlers.HandlesCustomerAsync(data.StaffId, Guid.NewGuid(), CancellationToken.None));
    }
}
