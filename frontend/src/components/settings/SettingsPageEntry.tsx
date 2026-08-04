import { StaffEntry } from "@/components/staff/StaffEntry";
import { SettingsClientEntry } from "@/components/settings/SettingsClientEntry";
import { hasSettingsAccess } from "@/lib/settings/settings";
import {
  settingsSelectionHref,
  type SettingsSelectionPath,
} from "@/lib/settings/settings-location";
import type { SearchParams } from "@/lib/query";

const baseSelection: SettingsSelectionPath = { section: "settings", id: "" };

// The settings form, both lists and their selected-record paths render the same Administrator workspace.
export function SettingsPageEntry({
  searchParams,
  selection = baseSelection,
}: {
  searchParams: Promise<SearchParams>;
  selection?: SettingsSelectionPath;
}) {
  return (
    <StaffEntry
      path={settingsSelectionHref(selection)}
      searchParams={searchParams}
      access={hasSettingsAccess}
      deniedTitle="Settings access unavailable"
      deniedDescription="Only Administrators can manage settings, promos and warranty packages."
    >
      {async () => {
        return (
          <SettingsClientEntry
            key={crypto.randomUUID()}
            parameters={await searchParams}
            selection={selection}
          />
        );
      }}
    </StaffEntry>
  );
}
