using Dealership.Application.Sales;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Development;

// Drives each demo sale through the same commands staff use. Every step has a fixed operation and base version,
// so a rerun replays completed steps instead of repeating them, and a sale changed by hand is reported, not reset.
public sealed class DevelopmentSaleFixtures(DealershipDbContext database, SaleHandlingService handling,
    SaleTermsService terms)
{
    public async Task<IReadOnlyList<DevelopmentFixtureReport>> RunAsync(bool seed, CancellationToken cancellationToken)
    {
        var reports = new List<DevelopmentFixtureReport>();
        foreach (var scenario in DevelopmentSaleScenarios.All)
        {
            reports.Add(await RunOneAsync(seed, scenario, cancellationToken));
        }

        return reports;
    }

    private async Task<DevelopmentFixtureReport> RunOneAsync(bool seed, DevelopmentSaleScenario scenario,
        CancellationToken cancellationToken)
    {
        var label = $"Sale ({scenario.Hold.Listing.Intake.RegistrationPlate}; {scenario.Target})";
        var sale = await database.SalesOrders.AsNoTracking()
            .Where(item => item.Id == scenario.ReservationId)
            .Select(item => new { item.Status, item.Version }).SingleOrDefaultAsync(cancellationToken);
        if (sale is null)
        {
            return new(label, "hold missing; preserved", true);
        }

        // An open hold is only worked from its first version; one moved on by hand is reported, not reset.
        if (seed && sale.Status != scenario.Target && !await DriveAsync(scenario, cancellationToken))
        {
            return new(label, "sale changed elsewhere; preserved", true);
        }

        var status = await database.SalesOrders.Where(item => item.Id == scenario.ReservationId)
            .Select(item => item.Status).SingleAsync(cancellationToken);
        return status == scenario.Target
            ? new(label, sale.Status == scenario.Target ? "matches" : "created", false)
            : new(label, $"{status}; preserved", true);
    }
    // Each step names the version it starts from; a replayed step succeeds with its original receipt.
    private async Task<bool> DriveAsync(DevelopmentSaleScenario scenario, CancellationToken cancellationToken)
    {
        var id = scenario.ReservationId;
        var agent = scenario.AgentId;
        var steps = new List<Func<Task<SaleCommandResult>>>
        {
            () => handling.ClaimAsync(agent, id, new(scenario.OperationId("claim"), 1), cancellationToken),
            () => terms.SaveTermsAsync(agent, id,
                new(scenario.OperationId("terms"), 2, null, scenario.Discount, null, 0m), cancellationToken)
        };
        if (scenario.ReleasedFirst)
        {
            steps.Add(() => handling.ReleaseAsync(agent, id, new(scenario.OperationId("release"), 3),
                cancellationToken));
            steps.Add(() => handling.AssignAsync(scenario.ManagerId, id,
                new(scenario.OperationId("assign"), 4, agent), cancellationToken));
            steps.Add(() => terms.SaveTermsAsync(agent, id,
                new(scenario.OperationId("terms-again"), 5, null, scenario.Discount, null, 0m), cancellationToken));
        }

        if (scenario.Target is SalesOrderStatus.PendingApproval or SalesOrderStatus.Approved)
        {
            steps.Add(() => terms.SubmitAsync(agent, id, new(scenario.OperationId("submit"), 3), cancellationToken));
        }

        foreach (var step in steps)
        {
            if ((await step()).Status != SaleStatus.Succeeded)
            {
                return false;
            }
        }

        return true;
    }
}
