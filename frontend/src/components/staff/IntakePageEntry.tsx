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
  const parameters = searchParams.then((query) =>
    selectedId ? { ...query, appointment: selectedId } : query,
  );
  return (
    <StaffEntry
      path={selectedId ? `/dashboard/intake/${selectedId}` : "/dashboard/intake"}
      searchParams={parameters}
      access={hasIntakeAccess}
      deniedTitle="Intake access unavailable"
      deniedDescription="This role cannot access intake appointments."
    >
      {async (actor) => (
        <IntakeClientEntry key={crypto.randomUUID()} actor={actor} parameters={await parameters} />
      )}
    </StaffEntry>
  );
}
