import { StaffEntry } from "@/components/staff/StaffEntry";
import { ListingClientEntry } from "@/components/vehicles/ListingClientEntry";
import { hasListingAccess } from "@/lib/vehicles/listings";
import type { SearchParams } from "@/lib/query";

export default async function ManagementPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  return (
    <StaffEntry
      path="/dashboard/management"
      access={hasListingAccess}
      deniedTitle="Listing access unavailable"
      deniedDescription="Only Branch Managers and General Managers can manage listings."
    >
      {async (actor) => {
        return (
          <ListingClientEntry
            key={crypto.randomUUID()}
            actor={actor}
            parameters={await searchParams}
          />
        );
      }}
    </StaffEntry>
  );
}
