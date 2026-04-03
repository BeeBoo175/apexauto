import { StaffEntry } from "@/components/staff/StaffEntry";
import { WorkshopClientEntry } from "@/components/inspection/WorkshopClientEntry";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import type { SearchParams } from "@/lib/query";

export default async function InspectionPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/inspection"
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async (actor) => {
        return (
          <WorkshopClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
