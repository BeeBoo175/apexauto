"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useStaffTestDriveEntry } from "@/hooks/use-staff-test-drive-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffTestDriveWorkspace } from "./StaffTestDriveWorkspace";

export function StaffTestDriveClientEntry({
  actor,
  parameters,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
}) {
  const { entry, failed, retry } = useStaffTestDriveEntry(actor, parameters);
  if (failed) {
    return (
      <StaffNotice title="Test drives">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry test drives
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <StaffTestDriveWorkspace actor={actor} entry={entry} />;
}
