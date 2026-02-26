"use client";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { useInspectionRepairRequest } from "@/hooks/use-inspection-repair-request";
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
  const request = useInspectionRepairRequest({
    actor,
    draft,
    latest,
    onLock,
    onConfirmed,
    onDraft,
  });
  const { work, budget, locked, dirty, command } = request;
  const findings = report.checklist.filter((point) => point.kind === "Finding");

  return (
    <details className={styles.category}>
      <summary>{latest ? "Revise repair work & budget" : "Request repair work"}</summary>
      <form
        className={styles.form}
        onSubmit={async (event) => {
          event.preventDefault();
          await request.submit();
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
                onChange={(e) => request.selectFinding(point.code, e.target.checked)}
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
                onChange={(e) => request.changeWork(point.code, e.target.value)}
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
          onChange={(e) => request.changeBudget(e.target.value)}
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
