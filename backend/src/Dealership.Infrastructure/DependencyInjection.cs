using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings__DefaultConnection"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<DealershipDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
            });
        }

        return services;
    }
}
