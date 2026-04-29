using Dealership.Domain.Customers;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>,
    IEntityTypeConfiguration<ReservationRelease>, IEntityTypeConfiguration<ReservationEvent>,
    IEntityTypeConfiguration<ReservationNumberCounter>
{
    public void Configure(EntityTypeBuilder<ReservationNumberCounter> builder)
    {
        builder.ToTable("ReservationNumberCounters", table => table.HasCheckConstraint("CK_ReservationNumberCounters_Shape",
            "\"Year\" BETWEEN 2000 AND 9999 AND \"LastValue\" BETWEEN 0 AND 999999"));
        builder.HasKey(item => item.Year);
        builder.Property(item => item.Year).ValueGeneratedNever();
    }

    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_Number", "\"Number\" ~ '^VRS-[0-9]{4}-[0-9]{6}$'");
            table.HasCheckConstraint("CK_Reservations_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "isfinite(\"CreatedAt\") AND isfinite(\"ExpiresAt\") AND " +
                "\"ExpiresAt\" >= \"CreatedAt\" + interval '48 hours' AND \"Deposit\" = 500");
            // A claim freezes the timer, so the three claim facts exist together and a hold never claims more than its period.
            table.HasCheckConstraint("CK_Reservations_Claim",
                "(\"ClaimedById\" IS NULL AND \"ClaimedAt\" IS NULL AND \"FrozenRemaining\" IS NULL) OR " +
                "(\"ClaimedById\" IS NOT NULL AND \"ClaimedAt\" IS NOT NULL AND \"ClaimedAt\" >= \"CreatedAt\" AND " +
                "\"FrozenRemaining\" > interval '0' AND \"FrozenRemaining\" <= interval '48 hours')");
            table.HasCheckConstraint("CK_Reservations_Price",
                "\"VehiclePrice\" > 0 AND \"VehiclePrice\" <> 'NaN'::numeric AND " +
                "\"ProcessingFee\" >= 0 AND \"ProcessingFee\" <> 'NaN'::numeric AND " +
                "\"RoadTax\" >= 0 AND \"RoadTax\" <> 'NaN'::numeric AND \"SstRate\" BETWEEN 0 AND 1 AND " +
                "\"ServiceTax\" = round(\"ProcessingFee\" * \"SstRate\", 2) AND " +
                "\"PromoDiscount\" BETWEEN 0 AND \"VehiclePrice\" AND " +
                "\"Total\" = \"VehiclePrice\" + \"ProcessingFee\" + \"RoadTax\" + \"ServiceTax\" - \"PromoDiscount\"");
            table.HasCheckConstraint("CK_Reservations_Promo",
                "(\"PromoId\" IS NULL AND \"PromoCode\" IS NULL AND \"PromoDiscount\" = 0) OR " +
                "(\"PromoId\" IS NOT NULL AND \"PromoCode\" IS NOT NULL AND length(\"PromoCode\") BETWEEN 1 AND 50)");
            table.HasCheckConstraint("CK_Reservations_Buyer",
                "\"BuyerName\" ~ '[^[:space:]]' AND \"BuyerEmail\" ~ '[^[:space:]]' AND " +
                "\"BuyerMobile\" ~ '[^[:space:]]' AND \"BuyerNric\" ~ '^[0-9]{12}$'");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<CustomerProfile>().WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ListingPublication>().WithMany()
            .HasForeignKey(item => new { item.PublicationId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromoCode>().WithMany().HasForeignKey(item => item.PromoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ClaimedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ClaimedById, item.ClaimedAt }).HasFilter("\"ClaimedById\" IS NOT NULL");
        builder.Property(item => item.Number).HasMaxLength(15).UseCollation("C");
        builder.HasIndex(item => item.Number).IsUnique();
        builder.HasIndex(item => item.CustomerId).IsUnique().HasFilter("\"IsActive\"");
        builder.HasIndex(item => item.VehicleId).IsUnique().HasFilter("\"IsActive\"");
        builder.HasIndex(item => new { item.CustomerId, item.CreatedAt, item.Id });
        builder.HasIndex(item => new { item.BranchId, item.CreatedAt, item.Id });
        builder.HasIndex(item => item.ExpiresAt).HasFilter("\"IsActive\"");
        builder.Property(item => item.PromoCode).HasMaxLength(PromoCode.CodeMaximumLength).UseCollation("C");
        builder.Property(item => item.SstRate).HasPrecision(29, 28);
        builder.Property(item => item.Deposit).HasPrecision(18, 2);
        builder.ComplexProperty(item => item.Buyer, buyer =>
        {
            buyer.IsRequired();
            buyer.Property(item => item.LegalName).HasColumnName("BuyerName").HasMaxLength(200).IsRequired();
            buyer.Property(item => item.Email).HasColumnName("BuyerEmail").HasMaxLength(256).IsRequired();
            buyer.Property(item => item.Mobile).HasColumnName("BuyerMobile").HasMaxLength(30).IsRequired();
            buyer.Property(item => item.Nric).HasColumnName("BuyerNric").HasMaxLength(12).IsRequired();
        });
        builder.ComplexProperty(item => item.Price, price =>
        {
            price.IsRequired();
            price.Property(item => item.VehiclePrice).HasColumnName("VehiclePrice").HasPrecision(18, 2);
            price.Property(item => item.ProcessingFee).HasColumnName("ProcessingFee").HasPrecision(18, 2);
            price.Property(item => item.RoadTax).HasColumnName("RoadTax").HasPrecision(18, 2);
            price.Property(item => item.ServiceTax).HasColumnName("ServiceTax").HasPrecision(18, 2);
            price.Property(item => item.PromoDiscount).HasColumnName("PromoDiscount").HasPrecision(18, 2);
            price.Property(item => item.Total).HasColumnName("Total").HasPrecision(20, 2);
        });
    }

    public void Configure(EntityTypeBuilder<ReservationRelease> builder)
    {
        builder.ToTable("ReservationReleases", table =>
        {
            table.HasCheckConstraint("CK_ReservationReleases_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND " +
                "isfinite(\"RecordedAt\") AND \"Refund\" = 500 AND " +
                "\"Kind\" IN ('CustomerCancelled', 'StaffCancelled', 'Expired', 'Withdrawn', 'Reinspection', 'DealCancelled')");
            table.HasCheckConstraint("CK_ReservationReleases_Actor",
                "(\"Kind\" = 'CustomerCancelled' AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NULL) OR " +
                "(\"Kind\" IN ('StaffCancelled', 'DealCancelled') AND \"ActorId\" IS NOT NULL AND \"Reason\" IS NOT NULL AND " +
                "length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]') OR " +
                "(\"Kind\" IN ('Expired', 'Withdrawn', 'Reinspection') AND \"ActorId\" IS NULL AND \"Reason\" IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasOne<Reservation>().WithOne().HasForeignKey<ReservationRelease>(item => item.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.Refund).HasPrecision(18, 2);
    }

    public void Configure(EntityTypeBuilder<ReservationEvent> builder)
    {
        builder.ToTable("ReservationEvents", table =>
        {
            table.HasCheckConstraint("CK_ReservationEvents_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAtUtc\") AND " +
                "\"Sequence\" > 0 AND \"Kind\" IN ('Claimed', 'Assigned', 'Reassigned', 'Released') AND " +
                "((\"Kind\" = 'Claimed' AND \"AgentId\" = \"ActorId\" AND \"PreviousAgentId\" IS NULL) OR " +
                "(\"Kind\" = 'Assigned' AND \"AgentId\" IS NOT NULL AND \"PreviousAgentId\" IS NULL) OR " +
                "(\"Kind\" = 'Reassigned' AND \"AgentId\" IS NOT NULL AND \"PreviousAgentId\" IS NOT NULL AND " +
                "\"AgentId\" <> \"PreviousAgentId\") OR " +
                "(\"Kind\" = 'Released' AND \"AgentId\" IS NULL AND \"PreviousAgentId\" IS NOT NULL))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => new { item.ReservationId, item.Sequence }).IsUnique();
        builder.Property(item => item.Kind).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.AgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.PreviousAgentId).OnDelete(DeleteBehavior.Restrict);
    }
}
