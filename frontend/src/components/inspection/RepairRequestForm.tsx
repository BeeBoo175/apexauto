"use client";
import { useId, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { Input, Textarea } from "@/components/ui/Input";
import { useInspectionRepairRequest } from "@/hooks/use-inspection-repair-request";
import type { CurrentActor } from "@/lib/auth/actor";
import { label, type Draft } from "@/lib/inspection/contracts";
import type { Report, RepairRequest } from "@/lib/inspection/report-contracts";
import styles from "./Inspection.module.css";
import workshop from "./Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import { SignedFinding } from "./SignedFinding";
import { InspectionPointStatus } from "./WorkshopStatus";
import tones from "@/components/ui/StatusTone.module.css";

export function RepairRequestForm({
  actor,
  draft,
  report,
  latest,
  disabled,
  onLock,
  onConfirmed,
  onCancel,
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
  onCancel: () => void;
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
  const selected = findings.find((point) => point.code === selectedCode) ?? findings[0];
  const multipleFindings = findings.length > 1;
  const formId = useId();
  if (!active) {
    return null;
  }
  const actions = (
    <>
      <Button
        size="compact"
        type="button"
        variant="secondary"
        disabled={disabled || locked || Boolean(command.receipt)}
        onClick={() => {
          if (request.reset()) {
            onCancel();
          }
        }}
      >
        {dirty ? "Discard changes" : "Cancel"}
      </Button>
      <Button
        size="compact"
        type="submit"
        form={formId}
        disabled={command.busy || Boolean(command.receipt) || (disabled && !command.uncertain)}
      >
        {command.uncertain ? "Reconcile request" : "Submit request"}
      </Button>
    </>
  );

  return (
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
      <p className={workbench.help}>
        Cover every required finding before listing. Any new request removes availability until
        completed. Changed work or a higher budget requires fresh approval.
      </p>
      <div className={workshop.split} data-single-item={!multipleFindings}>
        {multipleFindings && (
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
                <span className={workshop.pointCode}>{point.code}</span>
                <strong>{point.description}</strong>
                <Badge
                  className={`${tones.badge} ${point.code in work ? tones.info : tones.neutral} ${tones.label}`}
                >
                  {point.code in work ? "Included" : "Not included"}
                </Badge>
              </Button>
            ))}
          </nav>
        )}
        {selected && (
          <section className={workshop.detail} aria-label="Edit selected repair work">
            <div className={workshop.detailHeading}>
              <div>
                <span className={workshop.pointContext}>
                  <span className={workshop.pointCode}>{selected.code}</span> ·{" "}
                  {label(selected.category)}
                </span>
                <h3>{selected.description}</h3>
                {selected.severity && (
                  <div>
                    <InspectionPointStatus kind="Finding" severity={selected.severity} />
                  </div>
                )}
              </div>
            </div>
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
              Include in request
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
          </section>
        )}
      </div>
      <div className={styles.repairBudget}>
        <Input
          id={`${formId}-budget`}
          label="Requested budget (MYR)"
          inputMode="decimal"
          required
          value={budget}
          disabled={locked || disabled}
          onChange={(e) => request.changeBudget(e.target.value)}
        />
      </div>
      {command.uncertain && (
        <p role="alert">
          Repair request is unconfirmed. Reconcile the captured work and budget with the same
          operation.
        </p>
      )}
      {actionsContainer ? (
        createPortal(actions, actionsContainer)
      ) : (
        <div className={styles.actions}>{actions}</div>
      )}
      {dirty && (
        <p className={styles.help}>
          Your work and budget edits remain here when switching sections or refreshing.
        </p>
      )}
    </form>
  );
}
