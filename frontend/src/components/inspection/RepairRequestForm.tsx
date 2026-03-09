"use client";
import { useId, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { useInspectionRepairRequest } from "@/hooks/use-inspection-repair-request";
import type { CurrentActor } from "@/lib/auth/actor";
import type { Draft } from "@/lib/inspection/contracts";
import type { Report, RepairRequest } from "@/lib/inspection/report-contracts";
import styles from "./Inspection.module.css";
import workshop from "./Workshop.module.css";
import { SignedFinding } from "./SignedFinding";

export function RepairRequestForm({
  actor,
  draft,
  report,
  latest,
  disabled,
  onLock,
  onConfirmed,
  onDraft,
  active = true,
  actionsContainer,
}: {
  actor: CurrentActor;
  draft: Draft;
  report: Report;
  latest: RepairRequest | null;
  disabled: boolean;
  onLock: (locked: boolean) => void;
  onConfirmed: () => Promise<void>;
  onDraft: (dirty: boolean) => void;
  active?: boolean;
  actionsContainer?: HTMLElement | null;
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
  const [selectedCode, setSelectedCode] = useState(findings[0]?.code ?? "");
  const [expanded, setExpanded] = useState(!latest);
  const selected = findings.find((point) => point.code === selectedCode) ?? findings[0];
  const formId = useId();
  if (!active) {
    return null;
  }
  const submit = (
    <Button
      size="compact"
      type="submit"
      form={formId}
      disabled={command.busy || Boolean(command.receipt) || (disabled && !command.uncertain)}
    >
      {command.uncertain ? "Reconcile repair request" : "Submit repair request"}
    </Button>
  );

  return (
    <details
      className={styles.category}
      open={expanded}
      onToggle={(e) => setExpanded(e.currentTarget.open)}
    >
      <summary>{latest ? "Revise repair work & budget" : "Request repair work"}</summary>
      <form
        id={formId}
        className={styles.form}
        onSubmit={async (event) => {
          event.preventDefault();
          const incomplete = await request.submit();
          if (incomplete) {
            setSelectedCode(incomplete);
          }
        }}
      >
        <p>
          Cover every required finding before listing. Any new request removes availability until
          completed. Changed work or a higher budget requires fresh approval.
        </p>
        <div className={workshop.split}>
          <nav className={workshop.index} aria-label="Repair findings">
            {findings.map((point) => (
              <Button
                key={point.code}
                type="button"
                size="compact"
                variant="ghost"
                className={workshop.indexItem}
                aria-pressed={selected?.code === point.code}
                onClick={() => setSelectedCode(point.code)}
              >
                {point.code} · {point.description}
                {point.code in work ? " · Included" : ""}
              </Button>
            ))}
          </nav>
          {selected && (
            <div className={`${styles.repairItem} ${workshop.detail}`}>
              <SignedFinding
                point={selected}
                inspectionId={report.inspectionId}
                revision={report.revision}
              />
              <label className={styles.checkbox}>
                <input
                  type="checkbox"
                  checked={selected.code in work}
                  disabled={locked || (disabled && !command.uncertain)}
                  onChange={(e) => request.selectFinding(selected.code, e.target.checked)}
                />
                Include {selected.code} · {selected.description}
              </label>
              {selected.code in work && (
                <Textarea
                  label={`${selected.code} repair work`}
                  rows={2}
                  required
                  maxLength={1000}
                  disabled={locked || disabled}
                  value={work[selected.code]}
                  onChange={(e) => request.changeWork(selected.code, e.target.value)}
                />
              )}
            </div>
          )}
        </div>
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
        {actionsContainer && expanded ? createPortal(submit, actionsContainer) : submit}
        {dirty && (
          <p className={styles.help}>
            Work descriptions remain in this form during inspection refreshes.
          </p>
        )}
      </form>
    </details>
  );
}
