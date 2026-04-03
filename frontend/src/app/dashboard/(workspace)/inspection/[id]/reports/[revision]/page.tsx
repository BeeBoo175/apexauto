import { notFound } from "next/navigation";
import { StaffEntry } from "@/components/staff/StaffEntry";
import { SignedReportClientEntry } from "@/components/inspection/SignedReportClientEntry";
import { isUuid } from "@/lib/auth/contracts";
import { hasInspectionAccess } from "@/lib/inspection/contracts";

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
        return (
          <SignedReportClientEntry key={crypto.randomUUID()} id={id} revision={Number(revision)} />
        );
      }}
    </StaffEntry>
  );
}
