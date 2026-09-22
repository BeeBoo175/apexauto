using Dealership.Application.Accounts;
using Dealership.Domain.Accounts;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Accounts;

public sealed class BusinessEvidencePeriod(DealershipDbContext database) : IBusinessEvidencePeriod
{
    // A missing settings row means the approved default, exactly as the disposal executor applies it.
    public async Task<int> YearsAsync(CancellationToken cancellationToken)
    {
        var years = await database.RetentionSettings.AsNoTracking()
            .Select(settings => (int?)settings.BusinessEvidenceYears).SingleOrDefaultAsync(cancellationToken);
        return years ?? RetentionSettings.DefaultBusinessYears;
    }
}
