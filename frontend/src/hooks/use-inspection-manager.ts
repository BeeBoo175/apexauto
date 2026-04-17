"use client";

import { useState } from "react";
import { useApiCommand } from "./use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseReopening } from "@/lib/inspection/parse-report";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft } from "@/lib/inspection/contracts";

export function useInspectionManager({
  actor,
  draft,
  onLock,
  onConfirmed,
  onDraft,
  onClose,
}: {
  actor: CurrentActor;
  draft: Draft;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
  onClose: () => void;
}) {
  const [reason, setReason] = useState("");
  const [locked, setLocked] = useState(false);
  const command = useApiCommand(
    `drafts/${draft.id}/reopenings`,
    parseReopening,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === draft.id &&
      r.actorId === actor.userId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.expectedRevision === body.revision,
    inspectionFetch,
  );

  function lock(value: boolean) {
    setLocked(value);
    onLock(value);
  }

  function changeReason(value: string) {
    setReason(value);
    onDraft(Boolean(value));
  }

  function assignmentConfirmed() {
    onDraft(false);
    onClose();
    lock(false);
    void onConfirmed();
  }

  async function reopen() {
    lock(true);
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      assignmentVersion: draft.assignmentVersion,
      revision: draft.revision,
      reason: reason.trim(),
    });
    if (result) {
      lock(false);
    }
    if (result?.status === 200 && result.data) {
      onDraft(false);
      onClose();
      await onConfirmed();
    }
  }

  return { reason, locked, command, lock, changeReason, assignmentConfirmed, reopen };
}
