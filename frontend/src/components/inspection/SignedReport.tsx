import type { Report, RevisionHistory, RepairHistory } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffLink } from "@/components/staff/StaffLink";
import { Badge } from "@/components/ui/Badge";
import { RevisionHistoryView, RepairHistoryView } from "./InspectionHistory";
import { ReportPrintControls } from "./ReportPrintControls";
import { SignedReportChecklist } from "./SignedReportChecklist";
import styles from "./SignedReport.module.css";
import tones from "@/components/ui/StatusTone.module.css";

export function SignedReport({
  report,
  revisions,
  repairs,
}: {
  report: Report;
  revisions: RevisionHistory | null;
  repairs: RepairHistory | null;
}) {
  const { vehicle } = report;
  const findings = report.checklist.filter((point) => point.kind === "Finding").length;
  return (
    <div data-inspection-report>
      <div className={styles.controls}>
        <ReportPrintControls />
      </div>
      <article className={styles.report}>
        <div className={styles.controls}>
          <StaffLink prefetch={false} href={`/dashboard/inspection/${report.inspectionId}`}>
            Back to inspection
          </StaffLink>
        </div>
        <header className={styles.heading}>
          <div>
            <h1>Inspection report</h1>
            <p>
              <strong>{vehicle.registrationPlate}</strong> · {vehicle.manufactureYear}{" "}
              {vehicle.make} {vehicle.model} {vehicle.trim}
            </p>
          </div>
          <Badge
            className={`${tones.badge} ${report.assessment.isRejected ? tones.danger : tones.info}`}
          >
            {report.assessment.isRejected ? "Rejected" : `Grade ${report.assessment.grade}`}
          </Badge>
        </header>
        <dl className={styles.assessment}>
          <div>
            <dt>Original repair estimate</dt>
            <dd>{formatCurrency(report.assessment.estimatedRepairCost)}</dd>
          </div>
          <div>
            <dt>Signed checklist</dt>
            <dd>
              {report.checklist.length} points · {findings} findings
            </dd>
          </div>
          <div>
            <dt>Signed revision</dt>
            <dd>
              {report.revision} · {formatMalaysianDateTime(report.signedAt)}
            </dd>
          </div>
        </dl>
        <dl className={styles.metadata}>
          <div>
            <dt>Inspection</dt>
            <dd>{report.inspectionId}</dd>
          </div>
          <div>
            <dt>VIN</dt>
            <dd>{vehicle.vin}</dd>
          </div>
          <div>
            <dt>Recorded mileage</dt>
            <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
          </div>
          <div>
            <dt>Technician</dt>
            <dd>{report.technicianIdentity}</dd>
          </div>
          <div>
            <dt>Catalog</dt>
            <dd>{report.catalogVersion}</dd>
          </div>
          <div>
            <dt>Saved / assignment versions</dt>
            <dd>
              {report.saveVersion} / {report.assignmentVersion}
            </dd>
          </div>
          <div>
            <dt>Intake appointment</dt>
            <dd>{vehicle.intakeAppointmentId}</dd>
          </div>
          <div>
            <dt>Checked in (Malaysia)</dt>
            <dd>{formatMalaysianDateTime(vehicle.checkedInAt)}</dd>
          </div>
        </dl>
        <p className={styles.explanation}>
          This is the retained signed revision. Linked repair work does not change its original
          findings, estimates or grade.
        </p>
        <SignedReportChecklist report={report} />
        <footer className={styles.footer}>
          Signed by {report.technicianIdentity} · {formatMalaysianDateTime(report.signedAt)} ·
          Revision receipt {report.operationId}
        </footer>
        {repairs ? (
          <RepairHistoryView initial={repairs} />
        ) : (
          <p>Linked repair history is unavailable. Refresh to load it.</p>
        )}
        <div className={styles.controls}>
          {revisions ? (
            <RevisionHistoryView initial={revisions} />
          ) : (
            <p>Revision history is unavailable. Refresh to load it.</p>
          )}
        </div>
      </article>
    </div>
  );
}
