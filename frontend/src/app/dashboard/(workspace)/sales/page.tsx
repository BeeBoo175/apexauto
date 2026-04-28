import { StaffEntry } from "@/components/staff/StaffEntry";
import { SalesClientEntry } from "@/components/sales/SalesClientEntry";
import { hasSalesAccess } from "@/lib/sales/sales";
import type { SearchParams } from "@/lib/query";

export default async function SalesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  return (
    <StaffEntry
      path="/dashboard/sales"
      searchParams={searchParams}
      access={hasSalesAccess}
      deniedTitle="Sales access unavailable"
      deniedDescription="Only Sales Agents work holds and deals from the sales workspace."
    >
      {async (actor) => {
        return (
          <SalesClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
