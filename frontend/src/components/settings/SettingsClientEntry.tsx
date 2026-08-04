"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useSettingsClientEntry } from "@/hooks/use-settings-client-entry";
import type { SearchParams } from "@/lib/query";
import type { SettingsSelectionPath } from "@/lib/settings/settings-location";

export function SettingsClientEntry({
  parameters,
  selection,
}: {
  parameters: SearchParams;
  selection: SettingsSelectionPath;
}) {
  const { entry, failed, retry } = useSettingsClientEntry(parameters, selection);
  if (failed) {
    return (
      <StaffNotice title="Settings">
        <Button size="compact" variant="secondary" onClick={() => void retry()}>
          Retry settings
        </Button>
      </StaffNotice>
    );
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Workspace } = entry;
  return <Workspace entry={entry} />;
}
