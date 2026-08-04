import { StaffEntry } from "@/components/staff/StaffEntry";
import { WorkshopClientEntry } from "@/components/inspection/WorkshopClientEntry";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import {
  workshopEntryParameters,
  workshopSelectionHref,
  type WorkshopSelectionPath,
} from "@/lib/inspection/workshop-location";
import type { SearchParams } from "@/lib/query";

const baseSelection: WorkshopSelectionPath = {
  id: "",
  section: "inspection",
  request: null,
  category: "",
};

export function WorkshopPageEntry({
  searchParams,
  selection = baseSelection,
}: {
  searchParams: Promise<SearchParams>;
  selection?: WorkshopSelectionPath;
}) {
  const parameters = searchParams.then((query) => workshopEntryParameters(query, selection));
  return (
    <StaffEntry
      path={workshopSelectionHref(selection)}
      searchParams={parameters}
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async (actor) => (
        <WorkshopClientEntry
          key={crypto.randomUUID()}
          actor={actor}
          parameters={await parameters}
        />
      )}
    </StaffEntry>
  );
}
