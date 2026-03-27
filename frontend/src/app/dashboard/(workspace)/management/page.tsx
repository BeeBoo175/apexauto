import { StaffEntry } from "@/components/staff/StaffEntry";
import { ListingTable } from "@/components/vehicles/ListingTable";
import { actorIdentity } from "@/lib/auth/actor";
import { privateApiRead } from "@/lib/server/http/api";
import { staffBranches } from "@/lib/staff/server";
import { hasListingAccess, listingParams, listingQuery } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";
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
        const { filters, page } = listingParams(await searchParams);
        const [initial, branches] = await Promise.all([
          privateApiRead(`/api/staff/listings?${listingQuery(filters, page)}`, parseListings),
          staffBranches(actor),
        ]);
        return (
          <ListingTable
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
          />
        );
      }}
    </StaffEntry>
  );
}
