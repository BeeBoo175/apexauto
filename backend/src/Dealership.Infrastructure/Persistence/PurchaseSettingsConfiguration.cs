using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class PurchaseSettingsConfiguration : IEntityTypeConfiguration<PurchaseSettings>
{
    public void Configure(EntityTypeBuilder<PurchaseSettings> builder)
    {
        builder.ToTable("PurchaseSettings", table => table.HasCheckConstraint("CK_PurchaseSettings_Shape",
            "\"Id\" = 1 AND \"ProcessingFee\" BETWEEN 0 AND 5000 AND " +
            "\"SstRate\" BETWEEN 0 AND 0.20 AND \"MaxAgentDiscountAmount\" BETWEEN 0 AND 5000 AND " +
            "\"ReservationHoldHours\" BETWEEN 1 AND 168 AND \"Version\" > 0 AND isfinite(\"UpdatedAtUtc\") AND " +
            "\"UpdatedById\" <> '00000000-0000-0000-0000-000000000000'::uuid"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ProcessingFee).HasPrecision(18, 2);
        builder.Property(item => item.MaxAgentDiscountAmount).HasPrecision(18, 2).HasDefaultValue(500m);
        builder.Property(item => item.ReservationHoldHours).HasDefaultValue(48);
        // Every .NET decimal rate in [0, 1] fits exactly and remains readable after direct SQL writes.
        builder.Property(item => item.SstRate).HasPrecision(29, 28);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.UpdatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
