using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionRepairCompletionConfiguration : IEntityTypeConfiguration<InspectionRepairCompletion>
{
    public void Configure(EntityTypeBuilder<InspectionRepairCompletion> builder)
    {
        builder.ToTable("InspectionRepairCompletions", table =>
        {
            table.HasCheckConstraint("CK_InspectionRepairCompletions_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND " +
                "\"Revision\" > 0 AND \"RepairVersion\" > 0 AND isfinite(\"CompletedAt\")");
            table.HasCheckConstraint("CK_InspectionRepairCompletions_Notes",
                "\"Notes\" ~ '[^[:space:]]' AND \"Notes\" !~ '[[:cntrl:]]' AND length(\"Notes\") <= 1000");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Notes).HasMaxLength(1000).IsRequired();
        builder.HasIndex(item => item.RequestId).IsUnique();
        builder.HasIndex(item => new { item.InspectionId, item.RepairVersion }).IsUnique();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionRepairRequestRecord>().WithMany().HasForeignKey(item => item.RequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionRepairApproval>().WithMany().HasForeignKey(item => item.ApprovalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
