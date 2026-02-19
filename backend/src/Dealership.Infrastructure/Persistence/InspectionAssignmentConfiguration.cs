using Dealership.Domain.Inspections;
using Dealership.Domain.Vehicles;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class InspectionAssignmentConfiguration : IEntityTypeConfiguration<InspectionDraft>,
    IEntityTypeConfiguration<InspectionAssignment>
{
    public void Configure(EntityTypeBuilder<InspectionDraft> builder)
    {
        builder.ToTable("InspectionDrafts", table => table.HasCheckConstraint("CK_InspectionDrafts_Shape",
            "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AssignmentVersion\" > 0 AND \"Revision\" > 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.HasAlternateKey(item => new { item.Id, item.BranchId });
        builder.HasIndex(item => item.VehicleId).IsUnique();
        builder.HasIndex(item => new { item.BranchId, item.CreatedAt, item.Id });
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(item => new { item.VehicleId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(item => item.AssignmentVersion).IsConcurrencyToken();
        builder.Property(item => item.Revision).HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(item => item.IsSigned).HasDefaultValue(false).IsConcurrencyToken();
    }

    public void Configure(EntityTypeBuilder<InspectionAssignment> builder)
    {
        builder.ToTable("InspectionAssignments", table =>
        {
            table.HasCheckConstraint("CK_InspectionAssignments_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\"");
            table.HasCheckConstraint("CK_InspectionAssignments_Reason",
                "(\"ExpectedVersion\" = 0 AND \"PreviousTechnicianId\" IS NULL AND \"Reason\" IS NULL AND \"ActorId\" = \"TechnicianId\") OR " +
                "(\"ExpectedVersion\" > 0 AND \"PreviousTechnicianId\" IS NOT NULL AND \"PreviousTechnicianId\" <> \"TechnicianId\" AND \"Reason\" IS NOT NULL AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasIndex(item => new { item.InspectionId, item.Version }).IsUnique();
        builder.HasOne<InspectionDraft>().WithMany().HasForeignKey(item => new { item.InspectionId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.PreviousTechnicianId).OnDelete(DeleteBehavior.Restrict);
    }
}
