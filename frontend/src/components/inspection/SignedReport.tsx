import type { Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffLink } from "@/components/staff/StaffLink";
import { Badge } from "@/components/ui/Badge";
import { ReportPrintControls } from "./ReportPrintControls";
import { SignedReportChecklist } from "./SignedReportChecklist";
import { SignedReportFindings } from "./SignedReportFindings";
import styles from "./SignedReport.module.css";
import tones from "@/components/ui/StatusTone.module.css";

export function SignedReport({ report }: { report: Report }) {
  const { vehicle } = report;
  const findings = report.checklist.filter((point) => point.kind === "Finding");
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
            <dt>Findings</dt>
            <dd>{findings.length}</dd>
          </div>
          <div>
            <dt>Points checked</dt>
            <dd>{report.checklist.length}</dd>
          </div>
          <div>
            <dt>Signed revision</dt>
            <dd>{report.revision}</dd>
          </div>
        </dl>
        <section className={styles.section}>
          <h2>Vehicle and examination</h2>
          <dl className={styles.metadata}>
            <div>
              <dt>VIN</dt>
              <dd>{vehicle.vin}</dd>
            </div>
            <div>
              <dt>Recorded mileage</dt>
              <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
            </div>
            <div>
              <dt>Checked in (Malaysia)</dt>
              <dd>{formatMalaysianDateTime(vehicle.checkedInAt)}</dd>
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
              <dt>Signed (Malaysia)</dt>
              <dd>{formatMalaysianDateTime(report.signedAt)}</dd>
            </div>
          </dl>
        </section>
        <SignedReportFindings inspectionId={report.inspectionId} findings={findings} />
        <SignedReportChecklist report={report} />
        <section className={styles.record}>
          <h2>Record details</h2>
          <dl>
            <div>
              <dt>Inspection</dt>
              <dd>{report.inspectionId}</dd>
            </div>
            <div>
              <dt>Revision receipt</dt>
              <dd>{report.operationId}</dd>
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
          </dl>
        </section>
        <footer className={styles.footer}>
          <p>
            Signed by {report.technicianIdentity} · {formatMalaysianDateTime(report.signedAt)}
          </p>
          <p>
            This is the retained signed revision. Linked repair work does not change its original
            findings, estimates or grade.
          </p>
        </footer>
      </article>
    </div>
  );
}
