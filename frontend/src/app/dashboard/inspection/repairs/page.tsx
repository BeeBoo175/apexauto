import { InspectionEntry } from "@/components/inspection/InspectionEntry";
import { RepairQueue } from "@/components/inspection/RepairQueue";
import { inspectionRead } from "@/lib/inspection/server";
import { parseRepairPage } from "@/lib/inspection/parse";
import { queueParams, queueQuery } from "@/lib/inspection/query";
import { intakeRead } from "@/lib/intake/server";
import { parseBranch, parsePage } from "@/lib/intake/parse";
import { actorIdentity } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { readEditor } from "@/lib/inspection/editor-server";

export default async function RepairsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
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
    <InspectionEntry path="/dashboard/inspection/repairs">
      {async (actor) => {
        const [initial, branches, selected] = await Promise.all([
          inspectionRead(`repair-requests?${queueQuery(filters, page)}`, parseRepairPage),
          actor.role === "GeneralManager"
            ? intakeRead("/api/branches?page=1&pageSize=25", (v) => parsePage(v, parseBranch))
            : null,
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
    </InspectionEntry>
  );
}
