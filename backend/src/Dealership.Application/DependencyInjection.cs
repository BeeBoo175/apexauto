using Dealership.Application.Vehicles;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ValuationService>();

        return services;
    }
}
