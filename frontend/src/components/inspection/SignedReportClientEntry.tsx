"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { StaffLink } from "@/components/staff/StaffLink";
import { Button } from "@/components/ui/Button";
import { useSignedReportClientEntry } from "@/hooks/use-signed-report-client-entry";

export function SignedReportClientEntry({ id, revision }: { id: string; revision: number }) {
  const { entry, failed, retry } = useSignedReportClientEntry(id, revision);
  if (failed) {
    return (
      <StaffNotice title="Signed report unavailable">
        <p>This revision is unavailable to this account or its response could not be confirmed.</p>
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry report
        </Button>
        <StaffLink prefetch={false} href={`/dashboard/inspection/${id}`}>
          Back to inspection
        </StaffLink>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { View, ...snapshot } = entry;
  return <View {...snapshot} />;
}
