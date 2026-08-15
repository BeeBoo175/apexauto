using Microsoft.EntityFrameworkCore;

namespace Dealership.IntegrationTests;

public sealed class CustomerSearchIndexTests(IntakePhotoFixture fixture) : IClassFixture<IntakePhotoFixture>
{
    [Fact]
    public async Task TrigramIndexesServeTheCustomerNameMobileAndEmailSearch()
    {
        await using var database = new Dealership.Infrastructure.Persistence.DealershipDbContext(
            new DbContextOptionsBuilder<Dealership.Infrastructure.Persistence.DealershipDbContext>()
                .UseNpgsql(fixture.Database.RuntimeConnectionString).Options);

        var indexes = await database.Database.SqlQuery<string>($"""
            SELECT indexname AS "Value" FROM pg_indexes
            WHERE indexname IN ('IX_CustomerProfiles_LegalName_Trigram', 'IX_CustomerProfiles_Mobile_Trigram',
                                'IX_AspNetUsers_Email_Trigram')
              AND indexdef LIKE '%USING gin%gin_trgm_ops%'
            ORDER BY indexname
            """).ToListAsync();

        Assert.Equal(["IX_AspNetUsers_Email_Trigram", "IX_CustomerProfiles_LegalName_Trigram",
            "IX_CustomerProfiles_Mobile_Trigram"], indexes);
    }
}
