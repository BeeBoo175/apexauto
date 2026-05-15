"use client";

import { useEffect, useState, type ComponentProps } from "react";
import { createPortal } from "react-dom";
import { StaffWorkspaceTabs } from "@/components/staff/StaffWorkspaceTabs";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { useSaleEditor } from "@/hooks/use-sale-editor";
import { useSaleVersion } from "@/hooks/use-sale-version";
import { useSaleVehicle } from "@/hooks/use-sale-vehicle";
import { useEditGuard } from "@/hooks/use-edit-guard";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import type { Sale } from "@/lib/sales/contracts";
import { isSaleVersionSection, saleSectionLabels, saleSections } from "@/lib/sales/sale-sections";
import { normalizeSaleReason } from "@/lib/sales/parse";
import { SaleAssignDialog } from "./SaleAssignDialog";
import { SaleFacts } from "./SaleFacts";
import { SaleVehicle } from "./SaleVehicle";
import { SaleFooter } from "./SaleFooter";
import { SaleDeal } from "./SaleDeal";
import { SalePastVersion } from "./SalePastVersion";
import { SaleRecord } from "./SaleRecord";
import { SaleTermsForm } from "./SaleTermsForm";
import { SaleTimeline } from "./SaleTimeline";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./Sales.module.css";

const tabs = saleSections.map((value) => ({
  value,
  label: saleSectionLabels[value],
  available: true,
}));

// The sale's record, terms, history and actions, hosted by the agent's Sales pane and the managers' drawer alike.
export function SaleEditor({
  sale,
  selection,
  now,
  actionsContainer = null,
  refreshControl,
  scrollWithHost = false,
  onChanged,
  onLocked,
}: {
  sale: Sale;
  selection: ReturnType<typeof useSaleSelection>;
  now: number;
  actionsContainer?: HTMLElement | null;
  refreshControl?: ComponentProps<typeof RefreshButton>;
  scrollWithHost?: boolean;
  // Tells the host to refresh what lists this sale; the sale itself is read again here.
  onChanged: () => void;
  onLocked?: (locked: boolean) => void;
}) {
  const editor = useSaleEditor(sale, onChanged);
  const { snapshot } = editor;
  const actions = snapshot.actions;
  const [assigning, setAssigning] = useState(false);
  const [footerActions, setFooterActions] = useState<HTMLDivElement | null>(null);
  // History is read only once its section has been opened.
  const [historyOpened, setHistoryOpened] = useState(selection.section === "history");
  const section = selection.section;
  // A past version replaces the current record on its section; History keeps its own list.
  const showsCurrent = selection.version === null;
  const viewingPast = !showsCurrent && isSaleVersionSection(section);
  const review = useSaleVersion(snapshot.id, viewingPast ? selection.version : null);
  const vehicle = useSaleVehicle(snapshot.id, section === "car");
  const { reason, setReason } = editor;
  const locked = assigning || editor.locked || editor.refreshing;
  useEditGuard({ pending: editor.refreshing, label: "Refreshing sale" });
  const noteNeeded = actions.decide;
  const disabled = editor.operating || editor.refreshing || editor.confirmed;
  const termsBlocked = editor.notesDirty || editor.commandBlocked("term-revisions");

  useEffect(() => {
    onLocked?.(locked);
    return () => onLocked?.(false);
  }, [locked, onLocked]);

  const refresh = {
    label: refreshControl?.label ?? "Refresh sale",
    busy: editor.refreshing || (section === "car" && vehicle.busy) || Boolean(refreshControl?.busy),
    disabled: editor.operating || Boolean(refreshControl?.disabled),
    onClick: () => {
      if (section === "car") {
        vehicle.retry();
      }
      void editor.refresh();
      refreshControl?.onClick();
    },
  };

  return (
    <>
      <LoadingProgress label="Updating sale" active={editor.busy || editor.refreshing} />
      {actionsContainer &&
        !viewingPast &&
        section !== "history" &&
        createPortal(<RefreshButton {...refresh} />, actionsContainer)}
      <div className={workbench.workspace} data-scroll-with-host={scrollWithHost || undefined}>
        <SaleRecord
          editor={editor}
          now={now}
          retained={review.result}
          loadingVersion={viewingPast && !review.result}
          versionFailed={review.failed}
        />
        <StaffWorkspaceTabs
          label="Sale sections"
          tabs={tabs}
          value={section}
          disabled={editor.operating}
          onChange={(next) => {
            selection.changeSection(next);
            setHistoryOpened((opened) => opened || next === "history");
          }}
        />
        <div className={workbench.body}>
          {viewingPast && (
            <SalePastVersion
              key={`${selection.version}:${section}`}
              review={review}
              section={section}
              context={snapshot.context}
              actionsContainer={actionsContainer}
              refreshControl={refresh}
            />
          )}
          {showsCurrent && section === "customer" && (
            <section className={styles.section}>
              <div className={workbench.sectionHeader}>
                <h2 className={workbench.sectionTitle}>Customer details</h2>
              </div>
              <SaleFacts context={snapshot.context} />
            </section>
          )}
          {section === "car" && (
            <SaleVehicle read={vehicle} saleId={snapshot.id} context={snapshot.context} />
          )}
          {showsCurrent && section === "deal" && (
            <section className={styles.section}>
              <div className={workbench.sectionHeader}>
                <h2 className={workbench.sectionTitle}>
                  {actions.saveTerms ? "Agree deal terms" : "Current deal"}
                </h2>
              </div>
              {editor.stale && (
                <div className={workbench.protocol} role="status">
                  <p>A newer version was loaded. Your entries are kept until you use it.</p>
                  <Button size="compact" variant="secondary" onClick={editor.useLatest}>
                    Use latest values
                  </Button>
                </div>
              )}
              <SaleDeal sale={snapshot.sale} context={snapshot.context}>
                {actions.saveTerms ? (
                  <SaleTermsForm
                    editor={editor}
                    disabled={termsBlocked}
                    actionsContainer={footerActions}
                  />
                ) : undefined}
              </SaleDeal>
              {noteNeeded && !editor.editingTerms && (
                <section className={workbench.stack}>
                  <Textarea
                    label="Return reason"
                    placeholder="Explain what the agent needs to revise"
                    maxLength={1000}
                    rows={3}
                    value={reason}
                    disabled={disabled || Boolean(editor.cancellationReason.trim())}
                    aria-invalid={Boolean(reason.trim()) && !normalizeSaleReason(reason)}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </section>
              )}
              {actions.cancel && !editor.editingTerms && (
                <section className={workbench.stack}>
                  <Textarea
                    label="Cancellation reason (optional)"
                    placeholder="Explain why this sale should be cancelled"
                    maxLength={1000}
                    rows={3}
                    value={editor.cancellationReason}
                    disabled={disabled || Boolean(reason.trim())}
                    aria-invalid={
                      Boolean(editor.cancellationReason.trim()) &&
                      !normalizeSaleReason(editor.cancellationReason)
                    }
                    onChange={(event) => editor.setCancellationReason(event.target.value)}
                  />
                  <p className={workbench.help}>
                    Leave blank to continue the sale. Entering a reason changes the main action to
                    Cancel sale, which requires confirmation.
                  </p>
                </section>
              )}
            </section>
          )}
          {historyOpened && (
            <div hidden={section !== "history"}>
              <SaleTimeline
                key={snapshot.sale.version}
                saleId={snapshot.id}
                disabled={editor.busy}
                active={section === "history"}
                actionsContainer={actionsContainer}
                refreshControl={refresh}
                onOpenVersion={(version, versionSection) =>
                  selection.openVersion(version, versionSection)
                }
              />
            </div>
          )}
        </div>
        <SaleFooter
          editor={editor}
          selection={selection}
          onAssign={() => setAssigning(true)}
          onActionsContainer={setFooterActions}
        />
      </div>
      {actions.assign && (
        <SaleAssignDialog editor={editor} open={assigning} onClose={() => setAssigning(false)} />
      )}
    </>
  );
}
