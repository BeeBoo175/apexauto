import { StaffEntry } from "@/components/staff/StaffEntry";
import { SalesClientEntry } from "@/components/sales/SalesClientEntry";
import { hasSalesAccess } from "@/lib/sales/contracts";
import {
  saleSelectionHref,
  salesEntryParameters,
  type SaleSelectionPath,
} from "@/lib/sales/sales-location";
import type { SearchParams } from "@/lib/query";

const baseSelection: SaleSelectionPath = { id: "", section: "deal", version: null };

export function SalesPageEntry({
  searchParams,
  selection = baseSelection,
}: {
  searchParams: Promise<SearchParams>;
  selection?: SaleSelectionPath;
}) {
  const parameters = searchParams.then((query) => salesEntryParameters(query, selection));
  return (
    <StaffEntry
      path={saleSelectionHref(selection)}
      searchParams={parameters}
      access={hasSalesAccess}
    >
      {async (actor) => (
        <SalesClientEntry key={crypto.randomUUID()} actor={actor} parameters={await parameters} />
      )}
    </StaffEntry>
  );
}
