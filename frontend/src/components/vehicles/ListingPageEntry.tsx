import { StaffEntry } from "@/components/staff/StaffEntry";
import { ListingClientEntry } from "@/components/vehicles/ListingClientEntry";
import {
  listingEntryParameters,
  listingSelectionHref,
  type ListingSelectionPath,
} from "@/lib/vehicles/listing-location";
import { hasListingAccess } from "@/lib/vehicles/listings";
import type { SearchParams } from "@/lib/query";

const baseSelection: ListingSelectionPath = { id: "", section: "listing", record: null };

export function ListingPageEntry({
  searchParams,
  selection = baseSelection,
}: {
  searchParams: Promise<SearchParams>;
  selection?: ListingSelectionPath;
}) {
  const parameters = searchParams.then((query) => listingEntryParameters(query, selection));
  return (
    <StaffEntry
      path={listingSelectionHref(selection)}
      searchParams={parameters}
      access={hasListingAccess}
    >
      {async (actor) => (
        <ListingClientEntry key={crypto.randomUUID()} actor={actor} parameters={await parameters} />
      )}
    </StaffEntry>
  );
}
