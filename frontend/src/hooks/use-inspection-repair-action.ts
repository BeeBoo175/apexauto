"use client";

import { useState } from "react";
import { useApiCommand } from "./use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseApproval, parseCompletion } from "@/lib/inspection/parse-report";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft, RepairRow } from "@/lib/inspection/contracts";
import type { Approval, Completion } from "@/lib/inspection/report-contracts";

export function useInspectionRepairAction({
  actor,
  draft,
  state,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  draft: Draft;
  state: RepairRow;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
}) {
  const [notes, setNotes] = useState("");
  const manager = actor.role === "BranchManager";
  const action = manager ? "repair-approvals" : "repair-completions";
  const parse = (v: unknown): Approval | Completion | null =>
    manager ? parseApproval(v) : parseCompletion(v);
  const command = useApiCommand(
    `drafts/${draft.id}/${action}`,
    parse,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === draft.id &&
      r.actorId === actor.userId &&
      r.requestId === body.requestId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.revision === body.revision &&
      r.repairVersion === body.repairVersion,
    inspectionFetch,
  );
  const actionable = manager
    ? state.status === "PendingApproval"
    : actor.role === "WorkshopTechnician" &&
      ["Approved", "ApprovalNotRequired"].includes(state.status);

  function changeNotes(value: string) {
    setNotes(value);
    onDraft(Boolean(value));
  }

  async function submit() {
    onLock(true);
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      requestId: state.requestId,
      assignmentVersion: state.assignmentVersion,
      revision: state.revision,
      repairVersion: state.repairVersion,
      ...(!manager ? { notes: notes.trim() } : {}),
    });
    if (result) {
      onLock(false);
    }
    if (result?.status === 200 && result.data) {
      onDraft(false);
      await onConfirmed();
    }
  }

  return { notes, manager, actionable, command, changeNotes, submit };
}
