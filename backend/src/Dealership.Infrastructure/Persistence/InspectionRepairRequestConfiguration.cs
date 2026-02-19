using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionRepairRequestConfiguration : IEntityTypeConfiguration<InspectionRepairRequestRecord>
{
    public void Configure(EntityTypeBuilder<InspectionRepairRequestRecord> builder)
    {
        builder.ToTable("InspectionRepairRequests", table =>
        {
            table.HasCheckConstraint("CK_InspectionRepairRequests_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND " +
                "\"Revision\" > 0 AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND " +
                "\"Version\" - 1 = \"ExpectedVersion\" AND \"Budget\" >= 0 AND isfinite(\"RequestedAt\")");
            table.HasCheckConstraint("CK_InspectionRepairRequests_Items", "valid_inspection_repair_items(\"ItemsJson\")");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ItemsJson).HasColumnType("jsonb");
        // Unconstrained numeric lets the CHECK reject fractional cents before any database rounding.
        builder.Property(item => item.Budget).HasColumnType("numeric");
        builder.ToTable(table => table.HasCheckConstraint("CK_InspectionRepairRequests_Budget",
            "\"Budget\" <= 9999999999999999.99 AND \"Budget\" = trunc(\"Budget\", 2)"));
        builder.HasIndex(item => new { item.InspectionId, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.SignedRevisionId, item.Version });
        builder.HasIndex(item => new { item.BranchId, item.RequestedAt, item.Id });
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionSignedRevision>().WithMany().HasForeignKey(item => item.SignedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
