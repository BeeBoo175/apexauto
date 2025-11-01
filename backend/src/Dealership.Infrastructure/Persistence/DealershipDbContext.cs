using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

public class DealershipDbContext(DbContextOptions<DealershipDbContext> options) : DbContext(options)
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
