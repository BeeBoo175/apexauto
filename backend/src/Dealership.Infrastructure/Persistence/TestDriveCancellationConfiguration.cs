using Dealership.Domain.TestDrives;
using Dealership.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class TestDriveCancellationConfiguration : IEntityTypeConfiguration<TestDriveCancellation>
{
    public void Configure(EntityTypeBuilder<TestDriveCancellation> builder)
    {
        builder.ToTable("TestDriveCancellations", table =>
        {
            table.HasCheckConstraint("CK_TestDriveCancellations_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\")");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => item.BookingId);
        builder.HasOne<TestDriveBooking>().WithOne().HasForeignKey<TestDriveCancellation>(item => new { item.BookingId, item.CustomerId })
            .HasPrincipalKey<TestDriveBooking>(item => new { item.Id, item.CustomerId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
