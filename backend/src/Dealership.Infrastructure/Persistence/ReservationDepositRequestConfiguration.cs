using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationDepositRequestConfiguration : IEntityTypeConfiguration<ReservationDepositRequest>
{
    public void Configure(EntityTypeBuilder<ReservationDepositRequest> builder)
    {
        builder.ToTable("ReservationDepositRequests", table => table.HasCheckConstraint("CK_ReservationDepositRequests_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND " +
            "\"ExpiresAt\" = \"CreatedAt\" + interval '24 hours' AND (\"ConfirmedAt\" IS NULL OR " +
            "(isfinite(\"ConfirmedAt\") AND \"ConfirmedAt\" >= \"CreatedAt\" AND \"ConfirmedAt\" < \"ExpiresAt\")) AND " +
            "(\"CancelledAt\" IS NULL OR (\"ConfirmedAt\" IS NULL AND isfinite(\"CancelledAt\") AND " +
            "\"CancelledAt\" >= \"CreatedAt\" AND \"CancelledAt\" < \"ExpiresAt\"))"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.BookedByStaffId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingPublication>().WithMany()
            .HasForeignKey(item => new { item.PublicationId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromoCode>().WithMany().HasForeignKey(item => item.PromoId).OnDelete(DeleteBehavior.Restrict);
        // Admission reads the unconfirmed requests of one car or one customer; lapse is judged against the clock.
        builder.HasIndex(item => new { item.VehicleId, item.ExpiresAt })
            .HasFilter("\"ConfirmedAt\" IS NULL AND \"CancelledAt\" IS NULL");
        builder.HasIndex(item => new { item.CustomerId, item.ExpiresAt })
            .HasFilter("\"ConfirmedAt\" IS NULL AND \"CancelledAt\" IS NULL");
    }
}
