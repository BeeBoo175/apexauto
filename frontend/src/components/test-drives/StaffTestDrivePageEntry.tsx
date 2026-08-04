import { StaffEntry } from "@/components/staff/StaffEntry";
import { StaffTestDriveClientEntry } from "@/components/test-drives/StaffTestDriveClientEntry";
import { hasTestDriveAccess } from "@/lib/test-drives/staff";
import type { SearchParams } from "@/lib/query";

export function StaffTestDrivePageEntry({
  searchParams,
  selectedId = "",
  section = "",
}: {
  searchParams: Promise<SearchParams>;
  selectedId?: string;
  section?: string;
}) {
  return (
    <StaffEntry
      path={selectedId ? `/dashboard/test-drives/${selectedId}` : "/dashboard/test-drives"}
      searchParams={searchParams}
      access={(actor) => hasTestDriveAccess(actor.role)}
      deniedTitle="Test drive access unavailable"
      deniedDescription="Only sales agents and managers can read test drives."
    >
      {async (actor) => (
        <StaffTestDriveClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await searchParams}
          selectedId={selectedId}
          section={section}
        />
      )}
    </StaffEntry>
  );
}
