import { notFound } from "next/navigation";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { SignedReport } from "@/components/inspection/SignedReport";
import { StaffNotice } from "@/components/staff/StaffPage";
import { StaffLink } from "@/components/staff/StaffLink";
import { inspectionRead } from "@/lib/inspection/server";
import { parseReport, parseRevisions, parseRepairs } from "@/lib/inspection/parse-report";
import { isUuid } from "@/lib/auth/contracts";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { paginationQuery } from "@/lib/pagination";

export const metadata = { title: "Signed inspection report | ApexAuto" };

export default async function ReportPage({
  params,
}: {
  params: Promise<{ id: string; revision: string }>;
}) {
  const { id, revision } = await params;
  if (
    !isUuid(id) ||
    !/^\d+$/.test(revision) ||
    !Number.isSafeInteger(Number(revision)) ||
    Number(revision) < 1
  ) {
    notFound();
  }
  const path = `/dashboard/inspection/${id}/reports/${revision}`;
  return (
    <StaffEntry
      path={path}
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async () => {
        const base = `reports/${id}/revisions`;
        const [report, revisions, repairs] = await Promise.all([
          inspectionRead(`${base}/${revision}`, parseReport),
          inspectionRead(`${base}?${paginationQuery()}`, parseRevisions),
          inspectionRead(`${base}/${revision}/repair-history?${paginationQuery()}`, parseRepairs),
        ]);
        if (
          !report.data ||
          report.data.inspectionId !== id ||
          report.data.revision !== Number(revision)
        ) {
          return (
            <StaffNotice title="Signed report unavailable">
              <p>
                This revision is unavailable to this account or its response could not be confirmed.
              </p>
              <StaffLink prefetch={false} href={path}>
                Retry report
              </StaffLink>
              <StaffLink prefetch={false} href={`/dashboard/inspection/${id}`}>
                Back to inspection
              </StaffLink>
            </StaffNotice>
          );
        }
        return (
          <SignedReport report={report.data} revisions={revisions.data} repairs={repairs.data} />
        );
      }}
    </StaffEntry>
  );
}
