import { StaffEntry } from "@/components/staff/StaffEntry";
import { DealsClientEntry } from "@/components/deals/DealsClientEntry";
import { hasDealAccess } from "@/lib/deals/deals";
import type { SearchParams } from "@/lib/query";

export default async function DealsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return (
    <StaffEntry
      path="/dashboard/deals"
      access={hasDealAccess}
      deniedTitle="Deal access unavailable"
      deniedDescription="Only Sales Agents, Branch Managers and General Managers can work with deals."
    >
      {async (actor) => {
        return (
          <DealsClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
