using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class ListingConfiguration : IEntityTypeConfiguration<ListingRevision>,
    IEntityTypeConfiguration<ListingPublication>, IEntityTypeConfiguration<ListingWithdrawal>
{
    public void Configure(EntityTypeBuilder<ListingRevision> builder)
    {
        builder.ToTable("ListingRevisions", table =>
        {
            table.HasCheckConstraint("CK_ListingRevisions_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Version\" > 0 AND " +
                "\"VehiclePrice\" > 0 AND \"RoadTax\" >= 0 AND isfinite(\"RecordedAt\") AND " +
                "length(btrim(\"Description\")) BETWEEN 1 AND 2000 AND \"Description\" = btrim(\"Description\") AND " +
                "\"Transmission\" IN ('Automatic', 'Manual') AND \"Fuel\" IN ('Petrol', 'Diesel', 'Hybrid', 'PlugInHybrid', 'Electric')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.VehicleId, item.BranchId });
        builder.HasIndex(item => new { item.VehicleId, item.Version }).IsUnique();
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(item => new { item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.VehiclePrice).HasPrecision(18, 2);
        builder.Property(item => item.RoadTax).HasPrecision(18, 2);
        builder.Property(item => item.Transmission).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Fuel).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.PhotoIds).HasDefaultValueSql("'{}'::uuid[]");
    }

    public void Configure(EntityTypeBuilder<ListingPublication> builder)
    {
        builder.ToTable("ListingPublications", table => table.HasCheckConstraint("CK_ListingPublications_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\")"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.VehicleId, item.BranchId });
        builder.HasIndex(item => item.RevisionId).IsUnique();
        builder.HasOne<ListingRevision>().WithMany().HasForeignKey(item => new { item.RevisionId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionSignedRevision>().WithMany().HasForeignKey(item => new { item.SignedRevisionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ListingWithdrawal> builder)
    {
        builder.ToTable("ListingWithdrawals", table => table.HasCheckConstraint("CK_ListingWithdrawals_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND isfinite(\"RecordedAt\") AND " +
            "length(btrim(\"Reason\")) BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" !~ '[[:cntrl:]]'"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasIndex(item => item.PublicationId).IsUnique();
        builder.HasOne<ListingPublication>().WithMany().HasForeignKey(item => new { item.PublicationId, item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.VehicleId, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
    }
}
