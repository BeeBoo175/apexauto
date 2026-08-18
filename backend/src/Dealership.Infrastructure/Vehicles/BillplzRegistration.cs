using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Infrastructure.Vehicles;

public static class BillplzRegistration
{
    // Registers Billplz as the payment and payout provider, with its settings checked when the API starts.
    public static IServiceCollection AddBillplzPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BillplzSettings>()
            .Bind(configuration.GetSection(BillplzSettings.SectionName))
            .Validate(settings => !settings.Enabled || settings.IsComplete,
                "Billplz needs an API key, X Signature key, collection id, HTTPS API root and public API address when enabled.")
            .ValidateOnStart();
        services.AddHttpClient<IPaymentGateway, BillplzPaymentGateway>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddHttpClient<IPayoutGateway, BillplzPayoutGateway>(client => client.Timeout = TimeSpan.FromSeconds(15));
        return services;
    }
}
