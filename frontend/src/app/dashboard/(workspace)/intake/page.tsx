import { StaffEntry } from "@/components/staff/StaffEntry";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { IntakeClientEntry } from "@/components/staff/IntakeClientEntry";
import type { SearchParams } from "@/lib/query";

export default async function IntakePage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/intake"
      searchParams={searchParams}
      access={hasIntakeAccess}
      deniedTitle="Intake access unavailable"
      deniedDescription="This role cannot access intake appointments."
    >
      {async (actor) => {
        return (
          <IntakeClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
