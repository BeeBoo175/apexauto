import { StaffEntry } from "@/components/staff/StaffEntry";
import { WorkshopQueue } from "@/components/inspection/WorkshopQueue";
import { TechnicianWorkspace } from "@/components/inspection/TechnicianWorkspace";
import { inspectionRead } from "@/lib/inspection/server";
import { parseWorkshopPage } from "@/lib/inspection/parse-workshop";
import {
  queueParams,
  queueQuery,
  technicianQueueParams,
  workshopSelection,
} from "@/lib/inspection/query";
import { staffBranches } from "@/lib/staff/server";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import type { SearchParams } from "@/lib/query";
import { actorIdentity } from "@/lib/auth/actor";
import { readEditor, readSelectedRepair } from "@/lib/inspection/editor-server";

export default async function InspectionPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const { filters, page } = queueParams(params, "workshop");
  const selected = workshopSelection(params);
  return (
    <StaffEntry
      path="/dashboard/inspection"
      access={hasInspectionAccess}
      deniedTitle="Inspection access unavailable"
      deniedDescription="This role cannot access inspections or repair requests."
    >
      {async (actor) => {
        if (actor.role === "WorkshopTechnician") {
          const ready = technicianQueueParams(params, "MyWork");
          const waiting = technicianQueueParams(params, "Waiting");
          const claimable = technicianQueueParams(params, "Claimable");
          const [readyJobs, waitingJobs, claimableJobs, inspection, review] = await Promise.all([
            inspectionRead(`workshop?${queueQuery(ready.filters, ready.page)}`, parseWorkshopPage),
            inspectionRead(
              `workshop?${queueQuery(waiting.filters, waiting.page)}`,
              parseWorkshopPage,
            ),
            inspectionRead(
              `workshop?${queueQuery(claimable.filters, claimable.page)}`,
              parseWorkshopPage,
            ),
            selected.id ? readEditor(selected.id) : null,
            selected.id && selected.request
              ? readSelectedRepair(selected.id, selected.request)
              : null,
          ]);
          return (
            <TechnicianWorkspace
              key={actorIdentity(actor)}
              actor={actor}
              queues={{
                MyWork: { ...ready, initial: readyJobs },
                Waiting: { ...waiting, initial: waitingJobs },
                Claimable: { ...claimable, initial: claimableJobs },
              }}
              initialClaim={params.view === "claim"}
              selectedId={selected.id}
              selectedInspection={inspection}
              selectedRequest={selected.request}
              selectedSection={selected.section}
              selectedReview={
                selected.request && review ? { selection: selected.request, result: review } : null
              }
            />
          );
        }
        const [initial, branches, inspection, review] = await Promise.all([
          inspectionRead(`workshop?${queueQuery(filters, page)}`, parseWorkshopPage),
          staffBranches(actor),
          selected.id ? readEditor(selected.id) : null,
          selected.id && selected.request
            ? readSelectedRepair(selected.id, selected.request)
            : null,
        ]);
        return (
          <WorkshopQueue
            key={actorIdentity(actor)}
            actor={actor}
            initial={initial}
            filters={filters}
            page={page}
            branches={branches?.data ?? null}
            selectedId={selected.id}
            selectedInspection={inspection}
            selectedRequest={selected.request}
            selectedSection={selected.section}
            selectedReview={
              selected.request && review ? { selection: selected.request, result: review } : null
            }
          />
        );
      }}
    </StaffEntry>
  );
}
