import type { AppointmentDetail } from "@/lib/staff/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffDetailsDialog, type StaffDetail } from "./StaffDetailsDialog";
import { AppointmentStatus } from "./AppointmentStatus";
import { StaffStatusGroup } from "./StaffStatusGroup";
import styles from "./StaffWorkspace.module.css";

export function IntakeRecord({
  detail,
  checkedInAt,
}: {
  detail: AppointmentDetail;
  checkedInAt?: string;
}) {
  const ownership = detail.ownership;
  const details: StaffDetail[] = [
    { label: "Appointment reference", value: detail.id },
    { label: "Declared mileage", value: `${detail.mileageKilometres.toLocaleString("en-MY")} km` },
  ];
  if (checkedInAt) {
    details.push({ label: "Checked in", value: formatMalaysianDateTime(checkedInAt) });
  }
  if (ownership) {
    details.push({
      label: "Ownership updated",
      value: formatMalaysianDateTime(ownership.recordedAt),
    });
  }
  if (ownership?.reason) {
    details.push({ label: "Handoff reason", value: ownership.reason });
  }
  if (detail.noShowAt) {
    details.push({ label: "No-show effective", value: formatMalaysianDateTime(detail.noShowAt) });
  }
  if (detail.attendanceCorrectedAt) {
    details.push(
      {
        label: "Attendance corrected",
        value: formatMalaysianDateTime(detail.attendanceCorrectedAt),
      },
      { label: "Correction reason", value: detail.correctionReason },
      { label: "Corrected by", value: detail.correctedByEmail ?? "Not on file" },
    );
  }

  return (
    <section className={styles.summary} aria-label="Appointment time and status">
      <div className={styles.summaryMain}>
        <div>
          <span className={styles.summaryLabel}>Inspection</span>
          <strong className={styles.summaryValue}>
            {formatMalaysianDateTime(detail.startsAt)}
          </strong>
        </div>
        <div className={styles.summaryContext}>
          <StaffStatusGroup label="Appointment status">
            <AppointmentStatus status={detail.status} />
          </StaffStatusGroup>
          <StaffDetailsDialog title="Appointment details" details={details} />
        </div>
      </div>
      <div>
        <span className={styles.summaryLabel}>Handling agent</span>
        <span>{ownership?.handlerEmail ?? "Unassigned"}</span>
      </div>
      {ownership?.handlerId && !ownership.handlerEligible && (
        <p>
          The recorded handler is no longer eligible. A branch manager must reassign this
          appointment.
        </p>
      )}
    </section>
  );
}
