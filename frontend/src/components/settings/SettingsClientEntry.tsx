"use client";

import { StaffLoading } from "@/components/staff/StaffPage";
import { StaffReloadNotice } from "@/components/staff/StaffReloadNotice";
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
  const { entry, failed } = useSettingsClientEntry(parameters, selection);
  if (failed) {
    return <StaffReloadNotice title="Settings" />;
  }
  if (!entry) {
    return <StaffLoading />;
  }
  const { Workspace } = entry;
  return <Workspace entry={entry} />;
}
