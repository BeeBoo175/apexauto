using Dealership.Domain.Inspections;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionDraftSaveConfiguration : IEntityTypeConfiguration<InspectionDraftSave>
{
    public void Configure(EntityTypeBuilder<InspectionDraftSave> builder)
    {
        builder.ToTable("InspectionDraftSaves", table =>
        {
            table.HasCheckConstraint("CK_InspectionDraftSaves_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND " +
                "\"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\"");
            table.HasCheckConstraint("CK_InspectionDraftSaves_Attestation",
                "\"ExaminedPointCodesJson\" IS NULL OR valid_inspection_attestation(\"EntriesJson\", \"ExaminedPointCodesJson\")");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.CatalogVersion).HasMaxLength(40);
        builder.Property(item => item.EntriesJson).HasColumnType("jsonb");
        builder.Property(item => item.PhotoIdsJson).HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(item => item.ExaminedPointCodesJson).HasColumnType("jsonb");
        builder.HasIndex(item => new { item.InspectionId, item.Version }).IsUnique();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
