using Dealership.Domain.Intake;
using Dealership.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dealership.Infrastructure.Persistence;

public sealed class AppointmentAssignmentConfiguration : IEntityTypeConfiguration<AppointmentAssignment>
{
    public void Configure(EntityTypeBuilder<AppointmentAssignment> builder)
    {
        builder.ToTable("AppointmentAssignments", table =>
        {
            table.HasCheckConstraint("CK_AppointmentAssignments_Shape",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ExpectedVersion\" >= 0 AND \"Version\" > 0 AND \"Version\" - 1 = \"ExpectedVersion\" AND isfinite(\"RecordedAt\")");
            table.HasCheckConstraint("CK_AppointmentAssignments_Transition",
                "(\"Kind\" = 'Claimed' AND \"HandlerId\" = \"ActorId\" AND \"HandlerId\" IS NOT NULL AND \"PreviousHandlerId\" IS NULL AND \"Reason\" IS NULL) OR " +
                "(\"Kind\" = 'Assigned' AND \"HandlerId\" IS NOT NULL AND \"HandlerId\" IS DISTINCT FROM \"PreviousHandlerId\" AND (\"PreviousHandlerId\" IS NULL OR \"Reason\" IS NOT NULL)) OR " +
                "(\"Kind\" = 'Released' AND \"HandlerId\" IS NULL AND \"PreviousHandlerId\" IS NOT NULL)");
            table.HasCheckConstraint("CK_AppointmentAssignments_Reason",
                "\"Reason\" IS NULL OR (length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" = btrim(\"Reason\") AND \"Reason\" ~ '[^[:space:]]' AND \"Reason\" !~ '[[:cntrl:]]')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Kind).HasMaxLength(16);
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.HasIndex(item => new { item.AppointmentId, item.Version }).IsUnique();
        builder.HasIndex(item => new { item.HandlerId, item.AppointmentId });
        builder.HasOne<IntakeAppointment>().WithMany().HasForeignKey(item => new { item.AppointmentId, item.BranchId })
            .HasPrincipalKey(item => new { item.Id, item.BranchId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.HandlerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AccountUser>().WithMany().HasForeignKey(item => item.PreviousHandlerId).OnDelete(DeleteBehavior.Restrict);
    }
}
