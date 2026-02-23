import { StaffEntry } from "@/components/staff/StaffEntry";
import { RepairQueue } from "@/components/inspection/RepairQueue";
import { inspectionRead } from "@/lib/inspection/server";
import { parseRepairPage } from "@/lib/inspection/parse";
import { queueParams, queueQuery } from "@/lib/inspection/query";
import { staffBranches } from "@/lib/staff/server";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import type { SearchParams } from "@/lib/query";
import { actorIdentity } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { readEditor } from "@/lib/inspection/editor-server";

export default async function RepairsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const { filters, page } = queueParams(params, "repair-requests");
  const selectedId = isUuid(params.inspection) ? params.inspection : "";
  const selectedRequest =
    isUuid(params.request) &&
    typeof params.revision === "string" &&
    /^\d+$/.test(params.revision) &&
    Number.isSafeInteger(Number(params.revision)) &&
    Number(params.revision) > 0
      ? { requestId: params.request, revision: Number(params.revision) }
      : null;
  return (
    <StaffEntry
      path="/dashboard/inspection/repairs"
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async (actor) => {
        const [initial, branches, selected] = await Promise.all([
          inspectionRead(`repair-requests?${queueQuery(filters, page)}`, parseRepairPage),
          staffBranches(actor),
          selectedId ? readEditor(selectedId) : null,
        ]);
        return (
          <RepairQueue
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
            selectedId={selectedId}
            selectedInspection={selected}
            selectedRequest={selectedRequest}
          />
        );
      }}
    </StaffEntry>
  );
}
