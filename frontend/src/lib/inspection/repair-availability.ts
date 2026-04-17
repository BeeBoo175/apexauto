import type { CurrentActor } from "@/lib/auth/actor";
import type { EditorSnapshot } from "./editor-snapshot";

export function canRequestRepair(snapshot: EditorSnapshot, actor: CurrentActor) {
  return Boolean(
    snapshot.draft.isSigned &&
    snapshot.report &&
    actor.role === "WorkshopTechnician" &&
    snapshot.draft.technicianId === actor.userId &&
    !snapshot.report.assessment.isRejected &&
    snapshot.report.checklist.some((point) => point.kind === "Finding"),
  );
}
