import { StaffEntry } from "@/components/staff/StaffEntry";
import { ListingTable } from "@/components/vehicles/ListingTable";
import { actorIdentity } from "@/lib/auth/actor";
import { privateApiRead } from "@/lib/server/http/api";
import { staffBranches } from "@/lib/staff/server";
import { hasListingAccess, listingParams, listingQuery } from "@/lib/vehicles/listings";
import { parseListings } from "@/lib/vehicles/parse-listings";
import { parseListingWorkspace } from "@/lib/vehicles/listing-workspace";
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
        const { filters, page, listingId, section } = listingParams(await searchParams);
        const [initial, branches, selected] = await Promise.all([
          privateApiRead(`/api/staff/listings?${listingQuery(filters, page)}`, parseListings),
          staffBranches(actor),
          listingId
            ? privateApiRead(`/api/staff/listings/${listingId}`, parseListingWorkspace)
            : null,
        ]);
        return (
          <ListingTable
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
            listingId={listingId}
            selected={
              selected?.data?.vehicle.vehicleId === listingId
                ? selected
                : { status: selected?.status ?? 200, data: null, message: "", uncertain: false }
            }
            section={section}
          />
        );
      }}
    </StaffEntry>
  );
}
