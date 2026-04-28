import { StaffEntry } from "@/components/staff/StaffEntry";
import { DealsClientEntry } from "@/components/deals/DealsClientEntry";
import { hasDealTableAccess } from "@/lib/deals/deals";
import type { SearchParams } from "@/lib/query";

export default async function DealsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return (
    <StaffEntry
      path="/dashboard/deals"
      access={hasDealTableAccess}
      deniedTitle="Deal access unavailable"
      deniedDescription="Only Branch Managers and General Managers can open the deals table. Sales Agents work deals from the sales workspace."
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
