import type { AppointmentDetail } from "@/lib/staff/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffDetailsDialog, type StaffDetail } from "./StaffDetailsDialog";
import { AppointmentStatus } from "./AppointmentStatus";
import { StaffStatusGroup } from "./StaffStatusGroup";
import { StaffRecordSummary, StaffSummaryFact } from "./StaffRecordSummary";
import { PersonLabel } from "./PersonLabel";

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
      { label: "Corrected by", value: detail.correctedByName ?? "Not on file" },
    );
  }

  return (
    <StaffRecordSummary
      ariaLabel="Appointment time and status"
      label="Inspection"
      value={formatMalaysianDateTime(detail.startsAt)}
      status={
        <StaffStatusGroup label="Appointment status">
          <AppointmentStatus status={detail.status} />
        </StaffStatusGroup>
      }
      context={<StaffDetailsDialog title="Appointment details" details={details} />}
      facts={
        <StaffSummaryFact label="Handling agent">
          {ownership?.handlerId && ownership.handlerEmail ? (
            <PersonLabel
              id={ownership.handlerId}
              name={ownership.handlerName ?? ownership.handlerEmail}
              identity={ownership.handlerEmail}
              pictureId={ownership.handlerPictureId}
            />
          ) : (
            <span>Unassigned</span>
          )}
        </StaffSummaryFact>
      }
    >
      {ownership?.handlerId && !ownership.handlerEligible && (
        <p>
          The recorded handler is no longer eligible. A branch manager must reassign this
          appointment.
        </p>
      )}
    </StaffRecordSummary>
  );
}
