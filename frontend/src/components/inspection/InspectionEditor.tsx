"use client";

import { useEffect, useState } from "react";
import { StaffLink } from "@/components/staff/StaffLink";
import { Button } from "@/components/ui/Button";
import { Badge } from "@/components/ui/Badge";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { Dialog } from "@/components/ui/Dialog";
import { useInspectionEditor } from "@/hooks/use-inspection-editor";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/intake/contracts";
import type { Technician } from "@/lib/inspection/contracts";
import { label, assessmentLabel } from "@/lib/inspection/contracts";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import { formatCurrency } from "@/lib/currency-format";
import { InspectionChecklist } from "./InspectionChecklist";
import { ManagerActions } from "./ManagerActions";
import { RepairPanel } from "./RepairPanel";
import { RevisionHistoryView } from "./InspectionHistory";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/AppointmentWorkbench.module.css";
import tones from "@/components/ui/StatusTone.module.css";

export function InspectionEditor({
  id,
  actor,
  initial,
  technicians,

  repairsOnly = false,
  onLocked,
  onChanged,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<EditorSnapshot>;
  technicians: Page<Technician> | null;
  actionsContainer?: HTMLElement | null;
  repairsOnly?: boolean;
  onLocked?: (locked: boolean) => void;
  onChanged?: () => void;
}) {
  const editor = useInspectionEditor(id, actor, initial, onChanged);
  const [confirm, setConfirm] = useState<"sign" | "discard" | null>(null);
  const { snapshot, base } = editor;
  const disabled = !editor.ready || editor.operating || editor.refreshing || editor.confirmed;
  const attested = Boolean(base?.latestSave?.examinedPointCodes);
  const locked = editor.dirty || editor.operating || editor.refreshing;

  useEffect(() => {
    onLocked?.(locked);
    return () => onLocked?.(false);
  }, [locked, onLocked]);
  return (
    <>
      <RefreshButton
        label="Refresh inspection"
        busy={editor.refreshing}
        disabled={!editor.ready || editor.operating}
        onClick={() => void editor.refresh()}
      />
      <div className={styles.workspace} inert={!editor.ready}>
        {!snapshot && (
          <section className={workbench.protocol}>
            <p>
              The inspection is unavailable to this account or the service could not be reached.
            </p>
            <Button
              size="compact"
              variant="secondary"
              disabled={editor.refreshing}
              onClick={() => void editor.refresh()}
            >
              Retry inspection
            </Button>
          </section>
        )}
        {snapshot && base && (
          <>
            <section className={workbench.workbench} aria-label="Vehicle and confirmed assessment">
              <div className={workbench.recordHeading}>
                <strong>{snapshot.draft.vehicle.registrationPlate}</strong>
                <Badge className={`${tones.badge} ${tones.info}`}>
                  {label(snapshot.draft.vehicle.status)}
                </Badge>
              </div>
              <dl className={workbench.details}>
                <div>
                  <dt>Branch</dt>
                  <dd>{snapshot.draft.vehicle.branchName}</dd>
                </div>
                <div>
                  <dt>Vehicle</dt>
                  <dd>
                    {snapshot.draft.vehicle.make} {snapshot.draft.vehicle.model}
                  </dd>
                </div>
                <div>
                  <dt>VIN</dt>
                  <dd className={workbench.reference}>{snapshot.draft.vehicle.vin}</dd>
                </div>
                <div>
                  <dt>Revision</dt>
                  <dd>
                    {snapshot.draft.revision} · {snapshot.draft.isSigned ? "Signed" : "Draft"}
                  </dd>
                </div>
                <div>
                  <dt>Mileage</dt>
                  <dd>{snapshot.draft.vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
                </div>
                <div>
                  <dt>Saved assessment</dt>
                  <dd>
                    {snapshot.draft.latestSave
                      ? `${assessmentLabel(snapshot.draft.latestSave.assessment)} · ${formatCurrency(snapshot.draft.latestSave.assessment.estimatedRepairCost)}`
                      : "No saved assessment"}
                  </dd>
                </div>
              </dl>
              {snapshot.report && (
                <StaffLink
                  prefetch={false}
                  target="_blank"
                  rel="noopener"
                  href={`/dashboard/inspection/${id}/reports/${snapshot.report.revision}`}
                >
                  Open signed report · revision {snapshot.report.revision}
                </StaffLink>
              )}
              {editor.dirty && (
                <p className={styles.help}>
                  Unsaved changes. Save or discard your draft before leaving. The assessment above
                  is the last backend-confirmed save.
                </p>
              )}
              {editor.stale && (
                <p role="alert">
                  A newer assignment, save or revision is available. Your draft remains retained.
                  Discard it to load the confirmed checklist before editing again.
                </p>
              )}
              {editor.confirmed && (
                <p role="alert">
                  The action was confirmed, but the latest inspection read is unavailable. Refresh
                  before another action.
                </p>
              )}
              {editor.pendingSnapshot && (
                <p role="status">
                  Fresh inspection data is available. Local form drafts remain retained. Finish or
                  discard them to use the fresh snapshot.
                </p>
              )}
              {editor.save.uncertain && (
                <p role="alert">
                  Save is unconfirmed. Reconcile the exact captured checklist and operation.
                </p>
              )}
              {editor.sign.uncertain && (
                <p role="alert">
                  Sign-off is unconfirmed. Reconcile the same saved version and revision.
                </p>
              )}
              {(editor.editable || editor.sign.uncertain || editor.dirty) && (
                <div className={workbench.actionFooter}>
                  <p className={workbench.help}>Save your findings before signing this revision.</p>
                  <div className={styles.actions}>
                    {editor.editable && (
                      <Button
                        size="compact"
                        disabled={
                          editor.save.busy ||
                          editor.sign.busy ||
                          editor.childLocked ||
                          editor.refreshing ||
                          editor.sign.uncertain
                        }
                        onClick={() => void editor.saveDraft()}
                      >
                        {editor.save.uncertain ? "Reconcile draft save" : "Save draft"}
                      </Button>
                    )}
                    {editor.editable && (
                      <Button
                        size="compact"
                        variant="secondary"
                        disabled={
                          disabled ||
                          editor.dirty ||
                          !attested ||
                          (base.latestSave?.version ?? 0) <= base.reopenedAfterSaveVersion
                        }
                        onClick={() => setConfirm("sign")}
                      >
                        Sign inspection
                      </Button>
                    )}
                    {editor.sign.uncertain && (
                      <Button
                        size="compact"
                        disabled={editor.sign.busy}
                        onClick={() => void editor.signDraft()}
                      >
                        Reconcile sign-off
                      </Button>
                    )}
                    {editor.dirty && (
                      <Button
                        size="compact"
                        variant="secondary"
                        disabled={editor.operating}
                        onClick={() => setConfirm("discard")}
                      >
                        Discard local changes
                      </Button>
                    )}
                  </div>
                </div>
              )}
            </section>
            {!repairsOnly && (
              <ManagerActions
                key={`manager:${editor.draftEpoch}:${snapshot.draft.revision}:${snapshot.draft.assignmentVersion}`}
                actor={actor}
                draft={snapshot.draft}
                technicians={technicians}
                disabled={disabled}
                onLock={editor.lockChild}
                onConfirmed={editor.actionConfirmed}
                onDraft={(dirty) => editor.markForm("manager", dirty)}
              />
            )}
            {!repairsOnly && (
              <InspectionChecklist
                draft={base}
                value={editor.value}
                actorId={actor.userId}
                editable={editor.editable}
                disabled={disabled}
                onChange={editor.change}
                onLock={editor.lockChild}
              />
            )}
            <RepairPanel
              key={`repairs:${editor.draftEpoch}`}
              actor={actor}
              snapshot={snapshot}
              disabled={disabled}
              onLock={editor.lockChild}
              onConfirmed={editor.actionConfirmed}
              onDraft={editor.markForm}
            />
            <RevisionHistoryView initial={snapshot.revisions} disabled={editor.operating} />
          </>
        )}
      </div>
      <Dialog
        open={Boolean(confirm)}
        title={confirm === "sign" ? "Sign this inspection revision" : "Discard local changes"}
        closeDisabled={editor.sign.busy || editor.sign.uncertain}
        onClose={() => setConfirm(null)}
      >
        <p>
          {confirm === "sign"
            ? "Sign the complete attested saved checklist and selected private evidence. The original findings, estimates and assessment are retained in an immutable report."
            : "Replace your local edits with the latest confirmed saved checklist. Unsaved changes will be lost."}
        </p>
        <div className={styles.actions}>
          <Button
            size="compact"
            variant="secondary"
            disabled={editor.sign.busy || editor.sign.uncertain}
            onClick={() => setConfirm(null)}
          >
            Keep editing
          </Button>
          <Button
            size="compact"
            disabled={editor.sign.busy}
            onClick={async () => {
              if (confirm === "discard") {
                editor.reset();
                setConfirm(null);
                return;
              }
              await editor.signDraft();
              setConfirm(null);
            }}
          >
            Confirm {confirm === "sign" ? "sign-off" : "discard"}
          </Button>
        </div>
      </Dialog>
    </>
  );
}
