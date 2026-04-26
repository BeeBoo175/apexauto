import { StaffEntry } from "@/components/staff/StaffEntry";
import { SettingsClientEntry } from "@/components/settings/SettingsClientEntry";
import { hasSettingsAccess } from "@/lib/settings/settings";
import type { SearchParams } from "@/lib/query";

export default async function SettingsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/settings"
      access={hasSettingsAccess}
      deniedTitle="Settings access unavailable"
      deniedDescription="Only Administrators can manage settings, promos and warranty packages."
    >
      {async () => {
        return <SettingsClientEntry key={crypto.randomUUID()} parameters={await searchParams} />;
      }}
    </StaffEntry>
  );
}
