using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionSignedRevisionConfiguration : IEntityTypeConfiguration<InspectionSignedRevision>
{
    public void Configure(EntityTypeBuilder<InspectionSignedRevision> builder)
    {
        builder.ToTable("InspectionSignedRevisions", table => table.HasCheckConstraint("CK_InspectionSignedRevisions_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Revision\" > 0 AND " +
            "\"AssignmentVersion\" > 0 AND \"SaveVersion\" > 0 AND isfinite(\"SignedAt\") AND " +
            "jsonb_typeof(\"ReportJson\") = 'object' AND octet_length(\"ReportJson\"::text) <= 4194304"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ReportJson).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.InspectionId, item.Revision }).IsUnique();
        builder.HasIndex(item => item.SaveId).IsUnique();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InspectionDraftSave>().WithMany().HasForeignKey(item => new { item.SaveId, item.InspectionId })
            .HasPrincipalKey(item => new { item.Id, item.InspectionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.TechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
