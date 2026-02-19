using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionReopeningConfiguration : IEntityTypeConfiguration<InspectionReopening>
{
    public void Configure(EntityTypeBuilder<InspectionReopening> builder)
    {
        builder.ToTable("InspectionReopenings", table =>
        {
            table.HasCheckConstraint("CK_InspectionReopenings_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND " +
                "\"ExpectedRevision\" > 0 AND \"Revision\" > 1 AND \"Revision\" - 1 = \"ExpectedRevision\" AND " +
                "\"AfterSaveVersion\" > 0 AND isfinite(\"RecordedAt\")");
            table.HasCheckConstraint("CK_InspectionReopenings_Reason",
                "\"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]' AND length(\"Reason\") <= 1000");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasIndex(item => new { item.InspectionId, item.Revision }).IsUnique();
        builder.HasIndex(item => item.SignedRevisionId).IsUnique();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionSignedRevision>().WithMany().HasForeignKey(item => item.SignedRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
