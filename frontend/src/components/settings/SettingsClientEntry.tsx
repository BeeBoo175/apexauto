"use client";

import { StaffLoading, StaffNotice } from "@/components/staff/StaffPage";
import { Button } from "@/components/ui/Button";
import { useSettingsClientEntry } from "@/hooks/use-settings-client-entry";
import type { SearchParams } from "@/lib/query";

export function SettingsClientEntry({ parameters }: { parameters: SearchParams }) {
  const { entry, failed, retry } = useSettingsClientEntry(parameters);
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
