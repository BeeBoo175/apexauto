import type { Report } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { StaffLink } from "@/components/staff/StaffLink";
import { ReportPrintControls } from "./ReportPrintControls";
import { SignedReportChecklist } from "./SignedReportChecklist";
import { SignedReportFindings } from "./SignedReportFindings";
import styles from "./SignedReport.module.css";

function plural(count: number, noun: string) {
  return `${count} ${noun}${count === 1 ? "" : "s"}`;
}

export function SignedReport({ report }: { report: Report }) {
  const { vehicle, assessment } = report;
  const findings = report.checklist.filter((point) => point.kind === "Finding");
  const notFitted = report.checklist.filter((point) => point.kind === "NotFitted");
  const passed = report.checklist.length - findings.length - notFitted.length;
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
              {vehicle.manufactureYear} {vehicle.make} {vehicle.model} {vehicle.trim}
            </p>
          </div>
          <p className={assessment.isRejected ? styles.rejected : styles.grade}>
            {assessment.isRejected ? "Rejected" : `Grade ${assessment.grade}`}
          </p>
        </header>
        <dl className={styles.identity}>
          <div>
            <dt>Registration</dt>
            <dd>{vehicle.registrationPlate}</dd>
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
            <dt>Checked in</dt>
            <dd>{formatMalaysianDateTime(vehicle.checkedInAt)}</dd>
          </div>
        </dl>
        <p className={styles.summary}>
          {plural(findings.length, "finding")} recorded with an original repair estimate of{" "}
          <strong>{formatCurrency(assessment.estimatedRepairCost)}</strong>. Of{" "}
          {report.checklist.length} checklist points, {passed} passed and {notFitted.length}{" "}
          {notFitted.length === 1 ? "is" : "are"} not fitted.
        </p>
        <SignedReportFindings inspectionId={report.inspectionId} findings={findings} />
        <SignedReportChecklist report={report} />
        <section className={styles.signoff}>
          <h2>Sign-off</h2>
          <dl>
            <div>
              <dt>Signed by</dt>
              <dd>{report.technicianIdentity}</dd>
            </div>
            <div>
              <dt>Signed</dt>
              <dd>{formatMalaysianDateTime(report.signedAt)}</dd>
            </div>
            <div>
              <dt>Revision</dt>
              <dd>{report.revision}</dd>
            </div>
            <div>
              <dt>Checklist catalog</dt>
              <dd>{report.catalogVersion}</dd>
            </div>
          </dl>
          <p>
            This is the retained signed revision. Linked repair work does not change its original
            findings, estimates or grade.
          </p>
          <dl className={styles.record}>
            <div>
              <dt>Inspection</dt>
              <dd>{report.inspectionId}</dd>
            </div>
            <div>
              <dt>Revision receipt</dt>
              <dd>{report.operationId}</dd>
            </div>
            <div>
              <dt>Intake appointment</dt>
              <dd>{vehicle.intakeAppointmentId}</dd>
            </div>
            <div>
              <dt>Saved / assignment versions</dt>
              <dd>
                {report.saveVersion} / {report.assignmentVersion}
              </dd>
            </div>
          </dl>
        </section>
      </article>
    </div>
  );
}
