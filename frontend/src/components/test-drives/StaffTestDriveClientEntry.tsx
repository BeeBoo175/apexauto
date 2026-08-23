"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
import { useStaffTestDriveEntry } from "@/hooks/use-staff-test-drive-entry";
import type { CurrentActor } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffTestDriveWorkspace } from "./StaffTestDriveWorkspace";

export function StaffTestDriveClientEntry({
  actor,
  parameters,
  selectedId,
  section,
}: {
  actor: CurrentActor;
  parameters: SearchParams;
  selectedId: string;
  section: string;
}) {
  const { entry, failed } = useStaffTestDriveEntry(actor, parameters, selectedId, section);
  if (failed) {
    return <StaffReloadNotice title="Test drives" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  return <StaffTestDriveWorkspace actor={actor} entry={entry} />;
}
