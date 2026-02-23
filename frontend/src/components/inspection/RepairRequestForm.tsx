"use client";
import { useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import { useApiCommand } from "@/hooks/use-api-command";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parseRepairRequest } from "@/lib/inspection/parse-report";
import { parseMyr } from "@/lib/inspection/query";
import { isRecord } from "@/lib/auth/contracts";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft } from "@/lib/inspection/contracts";
import type { Report, RepairRequest } from "@/lib/inspection/report-contracts";
import styles from "./Inspection.module.css";

export function RepairRequestForm({
  actor,
  draft,
  report,
  latest,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
}: {
  actor: CurrentActor;
  draft: Draft;
  report: Report;
  latest: RepairRequest | null;
  disabled: boolean;
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
  const findings = report.checklist.filter((point) => point.kind === "Finding");
  const dirty =
    Object.values(work).some(Boolean) || budget !== (latest?.budget.toFixed(2) ?? "0.00");
  return (
    <details className={styles.category}>
      <summary>{latest ? "Revise repair work & budget" : "Request repair work"}</summary>
      <form
        className={styles.form}
        onSubmit={async (e) => {
          e.preventDefault();
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
        }}
      >
        <p>
          Cover every required finding before listing. Any new request removes availability until
          completed. Changed work or a higher budget requires fresh approval.
        </p>
        {findings.map((point) => (
          <div key={point.code} className={styles.repairItem}>
            <label className={styles.checkbox}>
              <input
                type="checkbox"
                checked={point.code in work}
                disabled={locked || (disabled && !command.uncertain)}
                onChange={(e) => {
                  const next = { ...work };
                  if (e.target.checked) {
                    next[point.code] = "";
                  } else {
                    delete next[point.code];
                  }
                  setWork(next);
                  onDraft(true);
                }}
              />
              {point.code} · {point.description}
            </label>
            {point.code in work && (
              <Textarea
                label={`${point.code} repair work`}
                rows={2}
                required
                maxLength={1000}
                disabled={locked || disabled}
                value={work[point.code]}
                onChange={(e) => {
                  setWork({ ...work, [point.code]: e.target.value });
                  onDraft(true);
                }}
              />
            )}
          </div>
        ))}
        <Input
          label="Requested budget (MYR)"
          inputMode="decimal"
          required
          value={budget}
          disabled={locked || disabled}
          onChange={(e) => {
            setBudget(e.target.value);
            onDraft(true);
          }}
        />
        {command.uncertain && (
          <p role="alert">
            Repair request is unconfirmed. Reconcile the captured work and budget with the same
            operation.
          </p>
        )}
        <Button
          size="compact"
          type="submit"
          disabled={command.busy || Boolean(command.receipt) || (disabled && !command.uncertain)}
        >
          {command.uncertain ? "Reconcile repair request" : "Submit repair request"}
        </Button>
        {dirty && (
          <p className={styles.help}>
            Work descriptions remain in this form during inspection refreshes.
          </p>
        )}
      </form>
    </details>
  );
}
