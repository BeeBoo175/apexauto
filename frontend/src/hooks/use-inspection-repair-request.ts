"use client";

import { useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { useApiCommand } from "./use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseRepairRequest } from "@/lib/inspection/parse-report";
import { parseMyr } from "@/lib/currency-input";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft } from "@/lib/inspection/contracts";
import type { RepairRequest } from "@/lib/inspection/report-contracts";

export function useInspectionRepairRequest({
  actor,
  draft,
  latest,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  draft: Draft;
  latest: RepairRequest | null;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
}) {
  const [work, setWork] = useState<Record<string, string>>(() =>
    Object.fromEntries((latest?.items ?? []).map((item) => [item.pointCode, item.work])),
  );
  const [budget, setBudget] = useState(latest?.budget.toFixed(2) ?? "0.00");
  const toast = useToast();
  const command = useApiCommand(
    `drafts/${draft.id}/repair-requests`,
    parseRepairRequest,
    (r, body) =>
      isRecord(body) &&
      r.operationId === body.operationId &&
      r.inspectionId === draft.id &&
      r.actorId === actor.userId &&
      r.assignmentVersion === body.assignmentVersion &&
      r.revision === body.revision &&
      r.expectedVersion === body.expectedVersion,
    inspectionFetch,
  );
  const locked = command.busy || command.uncertain;
  const dirty =
    Object.values(work).some(Boolean) || budget !== (latest?.budget.toFixed(2) ?? "0.00");

  function selectFinding(pointCode: string, selected: boolean) {
    const next = { ...work };
    if (selected) {
      next[pointCode] = "";
    } else {
      delete next[pointCode];
    }
    setWork(next);
    onDraft(true);
  }

  function changeWork(pointCode: string, value: string) {
    setWork({ ...work, [pointCode]: value });
    onDraft(true);
  }

  function changeBudget(value: string) {
    setBudget(value);
    onDraft(true);
  }

  async function submit() {
    const amount = parseMyr(budget);
    const items = Object.entries(work)
      .filter(([, value]) => value.trim())
      .map(([pointCode, value]) => ({ pointCode, work: value.trim() }));
    if (amount === null || !items.length) {
      toast.error(
        "Choose at least one finding, describe its work and enter an exact nonnegative MYR budget.",
      );
      return;
    }
    onLock(true);
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      assignmentVersion: draft.assignmentVersion,
      revision: draft.revision,
      expectedVersion: draft.repairVersion,
      items,
      budget: amount,
    });
    if (result) {
      onLock(false);
    }
    if (result?.data && result.status === 200) {
      onDraft(false);
      await onConfirmed();
    }
  }

  return {
    work,
    budget,
    locked,
    dirty,
    command,
    selectFinding,
    changeWork,
    changeBudget,
    submit,
  };
}
