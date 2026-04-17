"use client";
import { useEffect, useRef, useState, type ComponentProps } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { Dialog } from "@/components/ui/Dialog";
import { useInspectionEditor } from "@/hooks/use-inspection-editor";
import { useInspectionTimeline } from "@/hooks/use-inspection-timeline";
import { useWorkspaceTabs } from "@/hooks/use-workspace-tabs";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { canRequestRepair } from "@/lib/inspection/repair-availability";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ApiResult, Page } from "@/lib/api";
import type { Technician } from "@/lib/inspection/contracts";
import type { EditorSnapshot } from "@/lib/inspection/editor-snapshot";
import type { SelectedRepairReview } from "@/lib/inspection/repair-review";
import { InspectionRecord } from "./InspectionRecord";
import { WorkshopFooter } from "./WorkshopFooter";
import { InspectionChecklist } from "./InspectionChecklist";
import { ManagerActions } from "./ManagerActions";
import { RepairPanel } from "./RepairPanel";
import { InspectionTimeline } from "./InspectionTimeline";
import { RetainedRepairReview } from "./RetainedRepairReview";
import type { RepairSelection, WorkshopSection } from "@/lib/inspection/query";
import styles from "./Inspection.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import workshop from "./Workshop.module.css";

export function InspectionEditor({
  id,
  actor,
  initial,
  technicians,
  actionsContainer,
  refreshControl,
  scrollWithHost = false,
  section: requestedSection,
  onSection,
  onReview,
  onLocked,
  onChanged,
  selectedRequest = null,
  selectedReview = null,
}: {
  id: string;
  actor: CurrentActor;
  initial: ApiResult<EditorSnapshot>;
  technicians: Page<Technician> | null;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
  scrollWithHost?: boolean;
  section: WorkshopSection;
  onSection: (section: WorkshopSection) => void;
  onReview: (request: RepairSelection | null) => void;
  onLocked?: (locked: boolean) => void;
  onChanged?: () => void;
  selectedRequest?: RepairSelection | null;
  selectedReview?: SelectedRepairReview | null;
}) {
  const editor = useInspectionEditor(id, actor, initial, onChanged);
  const [confirm, setConfirm] = useState<"sign" | "discard" | null>(null);
  const [footerActions, setFooterActions] = useState<HTMLDivElement | null>(null);
  const { snapshot, base } = editor;
  const repairKey = `${editor.draftEpoch}:${snapshot?.draft.revision}:${snapshot?.draft.repairVersion}`;
  const [repairMode, setRepairMode] = useState({ key: repairKey, editing: false });
  const repairEditing = repairMode.key === repairKey && repairMode.editing;
  const repairAvailable = Boolean(
    !snapshot ||
    selectedRequest ||
    snapshot.currentRequest ||
    repairEditing ||
    (snapshot && canRequestRepair(snapshot, actor)),
  );
  const historyAvailable = Boolean(
    !snapshot ||
    snapshot.draft.isSigned ||
    snapshot.draft.revision > 1 ||
    (snapshot.revisions?.totalCount ?? 0) > 0,
  );
  const { section, tabs } = useWorkspaceTabs<WorkshopSection>(
    requestedSection,
    [
      { value: "inspection", label: "Inspection", available: true },
      { value: "repairs", label: "Repairs", available: repairAvailable },
      { value: "history", label: "History", available: historyAvailable },
    ],
    "inspection",
    onSection,
  );
  const timeline = useInspectionTimeline(snapshot, section === "history");
  const previousSigned = useRef(snapshot?.draft.isSigned);
  useEffect(() => {
    const signed = snapshot?.draft.isSigned;
    if (previousSigned.current === false && signed && actor.role === "WorkshopTechnician") {
      onReview(null);
    }
    previousSigned.current = signed;
  }, [snapshot?.draft.isSigned, actor.role, onReview]);
  const disabled = !editor.ready || editor.operating || editor.refreshing || editor.confirmed;
  const locked = editor.dirty || editor.operating || editor.refreshing;
  const historical =
    selectedRequest &&
    (selectedRequest.requestId !== snapshot?.repairState?.requestId ||
      selectedRequest.revision !== snapshot?.draft.revision);
  useEffect(() => {
    onLocked?.(locked);
    return () => onLocked?.(false);
  }, [locked, onLocked]);
  const refresh = {
    label: refreshControl?.label ?? "Refresh inspection",
    busy: editor.refreshing || Boolean(refreshControl?.busy),
    disabled: !editor.ready || editor.operating || Boolean(refreshControl?.disabled),
    onClick: () => {
      if (!editor.ready || editor.operating || editor.refreshing || refreshControl?.disabled) {
        return;
      }
      void editor.refresh();
      refreshControl?.onClick();
    },
  };
  return (
    <>
      <LoadingProgress label="Refreshing inspection" active={editor.refreshing} />
      {actionsContainer &&
        !(historical && section === "repairs") &&
        createPortal(<RefreshButton {...refresh} />, actionsContainer)}
      <div
        className={workshop.workspace}
        data-scroll-with-host={scrollWithHost || undefined}
        inert={!editor.ready}
      >
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
            <InspectionRecord id={id} editor={editor} />
            <StaffWorkspaceTabs
              label="Workshop sections"
              tabs={tabs}
              value={section}
              disabled={editor.operating}
              onChange={onSection}
            />
            <div className={workshop.body} data-section={section}>
              <InspectionChecklist
                draft={base}
                report={snapshot.report}
                value={editor.value}
                actorId={actor.userId}
                editable={editor.editable}
                disabled={disabled}
                active={section === "inspection"}
                onChange={editor.change}
                onLock={editor.lockChild}
                managerControls={
                  <ManagerActions
                    key={`manager:${editor.draftEpoch}:${snapshot.draft.revision}:${snapshot.draft.assignmentVersion}`}
                    actor={actor}
                    draft={snapshot.draft}
                    technicians={technicians}
                    disabled={disabled}
                    active={section === "inspection"}
                    onLock={editor.lockChild}
                    onConfirmed={editor.actionConfirmed}
                    onDraft={(dirty) => editor.markForm("manager", dirty)}
                  />
                }
              />
              {historical && section === "repairs" && (
                <RetainedRepairReview
                  key={selectedRequest.requestId}
                  inspectionId={id}
                  selection={selectedRequest}
                  actionsContainer={actionsContainer}
                  refreshControl={refresh}
                  initial={
                    selectedReview?.selection.requestId === selectedRequest.requestId &&
                    selectedReview.selection.revision === selectedRequest.revision
                      ? selectedReview.result
                      : null
                  }
                />
              )}
              <RepairPanel
                key={`repairs:${editor.draftEpoch}`}
                actor={actor}
                snapshot={snapshot}
                disabled={disabled}
                active={section === "repairs" && !historical}
                onLock={editor.lockChild}
                onConfirmed={editor.actionConfirmed}
                onDraft={editor.markForm}
                actionsContainer={footerActions}
                editing={repairEditing}
                onEditingChange={(editing) => setRepairMode({ key: repairKey, editing })}
              />
              {section === "history" && (
                <InspectionTimeline
                  snapshot={snapshot}
                  timeline={timeline}
                  disabled={editor.operating}
                  onSelect={onReview}
                />
              )}
            </div>
            <WorkshopFooter
              editor={editor}
              section={section}
              historical={Boolean(historical)}
              repairEditing={repairEditing}
              onConfirm={setConfirm}
              onActionsContainer={setFooterActions}
              onCurrentRepairs={() => onReview(null)}
            />
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
