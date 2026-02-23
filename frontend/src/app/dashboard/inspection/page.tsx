import { InspectionEntry } from "@/components/inspection/InspectionEntry";
import { InspectionQueue } from "@/components/inspection/InspectionQueue";
import { inspectionRead } from "@/lib/inspection/server";
import { parseInspectionPage } from "@/lib/inspection/parse";
import { queueParams, queueQuery } from "@/lib/inspection/query";
import { intakeRead } from "@/lib/intake/server";
import { parseBranch, parsePage } from "@/lib/intake/parse";
import { actorIdentity } from "@/lib/auth/actor";
import { isUuid } from "@/lib/auth/contracts";
import { readEditor } from "@/lib/inspection/editor-server";

export default async function InspectionPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const { filters, page } = queueParams(params, "vehicles");
  const selectedId = isUuid(params.inspection) ? params.inspection : "";
  return (
    <InspectionEntry path="/dashboard/inspection">
      {async (actor) => {
        const [initial, branches, selected] = await Promise.all([
          inspectionRead(`vehicles?${queueQuery(filters, page)}`, parseInspectionPage),
          actor.role === "GeneralManager"
            ? intakeRead("/api/branches?page=1&pageSize=25", (v) => parsePage(v, parseBranch))
            : null,
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
    </InspectionEntry>
  );
}
