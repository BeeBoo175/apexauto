using Dealership.Application.Accounts;
using Dealership.Infrastructure.Development;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.BrowserJourneys;

internal sealed class IntakeAssignmentProbe(string directory) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, continuation) =>
            {
                var commandPath = Path.Combine(directory, "assignment.txt");
                var command = File.ReadAllText(commandPath);
                if (context.Request.Path == "/api/auth/session" && command.Length > 0 && command != "group-after-export")
                {
                    await ApplyAsync(context.RequestServices, command, context.RequestAborted);
                    File.WriteAllText(commandPath, string.Empty);
                }
                if (context.Request.Path == "/api/staff/intake/appointments/export" && command == "group-after-export")
                {
                    context.Response.OnStarting(async () =>
                    {
                        await ApplyAsync(context.RequestServices, "group", context.RequestAborted);
                        File.WriteAllText(commandPath, string.Empty);
                    });
                }
                await continuation();
            });
            next(app);
        };
    }

    private static async Task ApplyAsync(IServiceProvider services, string command, CancellationToken cancellationToken)
    {
        var branchId = command switch
        {
            "second-branch" => DevelopmentFixtureManifest.Branches[1].Id,
            "group" => (Guid?)null,
            "restore" => DevelopmentFixtureManifest.Branches[0].Id,
            _ => throw new InvalidOperationException("Unknown disposable staff assignment probe.")
        };
        var database = services.GetRequiredService<DealershipDbContext>();
        var user = await database.Users.SingleAsync(account => account.Email == "aina.sofia@example.invalid", cancellationToken);
        user.BranchId = branchId;
        user.Role = command == "group" ? AccountRoles.GeneralManager : AccountRoles.SalesAgent;
        await database.SaveChangesAsync(cancellationToken);
    }
}
