import "server-only";
import { hasIntakeAccess } from "@/lib/auth/actor";
import type { SearchParams } from "@/lib/query";
import { StaffEntry } from "./StaffEntry";
import { IntakeClientEntry } from "./IntakeClientEntry";

export function IntakePageEntry({
  searchParams,
  selectedId = "",
}: {
  searchParams: Promise<SearchParams>;
  selectedId?: string;
}) {
  return (
    <StaffEntry
      path={selectedId ? `/dashboard/intake/${selectedId}` : "/dashboard/intake"}
      searchParams={searchParams}
      access={hasIntakeAccess}
    >
      {async (actor) => (
        <IntakeClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await searchParams}
          selectedId={selectedId}
        />
      )}
    </StaffEntry>
  );
}
