import { StaffEntry } from "@/components/staff/StaffEntry";
import { InspectionQueue } from "@/components/inspection/InspectionQueue";
import { inspectionRead } from "@/lib/inspection/server";
import { parseInspectionPage } from "@/lib/inspection/parse";
import { queueParams, queueQuery } from "@/lib/inspection/query";
import { staffBranches } from "@/lib/staff/server";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import type { SearchParams } from "@/lib/query";
import { actorIdentity } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { readEditor } from "@/lib/inspection/editor-server";

export default async function InspectionPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const { filters, page } = queueParams(params, "vehicles");
  const selectedId = isUuid(params.inspection) ? params.inspection : "";
  return (
    <StaffEntry
      path="/dashboard/inspection"
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async (actor) => {
        const [initial, branches, selected] = await Promise.all([
          inspectionRead(`vehicles?${queueQuery(filters, page)}`, parseInspectionPage),
          staffBranches(actor),
          selectedId ? readEditor(selectedId) : null,
        ]);
        return (
          <InspectionQueue
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
            selectedId={selectedId}
            selectedInspection={selected}
          />
        );
      }}
    </StaffEntry>
  );
}
