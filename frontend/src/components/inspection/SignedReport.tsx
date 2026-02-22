import type { Report } from "@/lib/inspection/report-contracts";
import { StaffNotice } from "@/components/staff/StaffPage";
export function SignedReport({ report }: { report: Report }) {
  return (
    <StaffNotice title="Signed inspection report">
      <p>
        {report.vehicle.registrationPlate} · Revision {report.revision}
      </p>
      <p>{report.assessment.isRejected ? "Rejected" : `Grade ${report.assessment.grade}`}</p>
      <p>Signed by {report.technicianIdentity}</p>
    </StaffNotice>
  );
}
