"use client";

import { useId, useState } from "react";
import { createPortal } from "react-dom";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import type { useSaleDocumentCorrection } from "@/hooks/use-sale-document-correction";
import forms from "@/components/staff/StaffForms.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function SaleDocumentCorrection({
  correction,
  actionsContainer,
  blocked,
}: {
  correction: ReturnType<typeof useSaleDocumentCorrection>;
  actionsContainer: HTMLElement | null;
  blocked: boolean;
}) {
  const formId = useId();
  const [confirming, setConfirming] = useState(false);
  const { draft } = correction;
  if (!draft) {
    return null;
  }
  const disabled = blocked || correction.operating;
  const actions = (
    <>
      <ConfirmationButton
        size="compact"
        variant="secondary"
        disabled={disabled}
        confirmationTitle="Discard this correction?"
        message="Your unsent buyer corrections and reason will be cleared."
        onConfirm={correction.discard}
      >
        Discard changes
      </ConfirmationButton>
      <Button
        size="compact"
        type="submit"
        form={formId}
        disabled={
          blocked ||
          correction.busy ||
          (!correction.uncertain && (!correction.valid || correction.stale))
        }
      >
        {correction.uncertain ? "Reconcile correction" : "Void and reissue"}
      </Button>
    </>
  );
  return (
    <form
      id={formId}
      className={forms.section}
      onSubmit={(event) => {
        event.preventDefault();
        if (
          blocked ||
          correction.busy ||
          (!correction.uncertain && (!correction.valid || correction.stale))
        ) {
          return;
        }
        if (correction.uncertain) {
          void correction.submit();
        } else {
          setConfirming(true);
        }
      }}
    >
      <h3>Correct {draft.document.number}</h3>
      <p className={forms.hint}>
        Only buyer name and address can change. The original document and its captured financial,
        identity and delivery facts are retained.
      </p>
      <Input
        label="Buyer name"
        density="compact"
        required
        maxLength={200}
        value={draft.buyerName}
        disabled={disabled}
        onChange={(event) => correction.change({ buyerName: event.target.value })}
      />
      <Input
        label="Buyer address"
        density="compact"
        required
        maxLength={1000}
        value={draft.buyerAddress}
        disabled={disabled}
        onChange={(event) => correction.change({ buyerAddress: event.target.value })}
      />
      <Textarea
        label="Correction reason"
        required
        maxLength={1000}
        rows={3}
        value={draft.reason}
        disabled={disabled}
        onChange={(event) => correction.change({ reason: event.target.value })}
      />
      {correction.stale && (
        <p className={forms.hint}>
          The sale changed after this correction was opened. Discard it and review the latest
          document.
        </p>
      )}
      <ConfirmationDialog
        open={confirming}
        title="Void and reissue this document?"
        confirmLabel="Void and reissue"
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          setConfirming(false);
          await correction.submit();
        }}
      >
        <p>
          The original remains available as superseded evidence. A new numbered document captures
          this correction and reason.
        </p>
      </ConfirmationDialog>
      {actionsContainer ? (
        createPortal(actions, actionsContainer)
      ) : (
        <div className={workbench.footerActions}>{actions}</div>
      )}
    </form>
  );
}
