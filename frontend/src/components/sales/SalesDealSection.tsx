"use client";

import { useState } from "react";
import { DealLines } from "@/components/deals/DealLines";
import { DealTermsForm } from "@/components/deals/DealTermsForm";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Textarea } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useToast } from "@/components/ui/Toast";
import { useSettingsSave } from "@/hooks/use-settings-save";
import type { CurrentActor } from "@/lib/auth/actor";
import { formatCurrency } from "@/lib/currency-format";
import { dealAbilities, validDealReason } from "@/lib/deals/deal-rules";
import { dealPath, dealStatusLabels, parseDeal, type Deal } from "@/lib/deals/deals";
import { holdsPath } from "@/lib/sales/sales";
import styles from "./Sales.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/settings/SettingsForms.module.css";

function footerNote(deal: Deal, dirty: boolean) {
  if (dirty) {
    return "Save your term changes before submitting.";
  }
  const notes: Record<Deal["status"], string> = {
    Draft: "Terms you agree with the customer are keyed here, then submitted.",
    PendingManagerApproval:
      "A manager approves or rejects this deal. You can follow it under Waiting.",
    Approved: "Approved. Settlement and handover follow.",
    Cancelled: "This deal is cancelled and its deposit refunded.",
  };
  return notes[deal.status];
}

// The deal's price and terms as the claiming agent works them: edit, submit, give the claim back or cancel.
export function SalesDealSection({
  deal,
  actor,
  onChanged,
  onReleased,
  onReload,
}: {
  deal: Deal;
  actor: CurrentActor;
  // Reads the deal and the queues again after a change.
  onChanged: () => Promise<void>;
  onReleased: () => void;
  onReload: () => void;
}) {
  const toast = useToast();
  const terms = useSettingsSave(parseDeal);
  const actions = useSettingsSave(parseDeal);
  const [opened, setOpened] = useState<Deal | null>(deal);
  const [revision, setRevision] = useState(0);
  const [reason, setReason] = useState("");
  const [dirty, setDirty] = useState(false);
  // The editor stays based on the first read of this deal until the agent chooses newer values.
  if (opened?.id !== deal.id) {
    setOpened(deal);
    setReason("");
  }
  const base = opened?.id === deal.id ? opened : null;
  const busy = terms.busy || actions.busy;
  const can = dealAbilities(deal, actor);
  const newer = base !== null && base.version !== deal.version;
  const reasonOk = validDealReason(reason);
  const releasable = can.edit && deal.status === "Draft" && deal.reservationId !== null;

  async function refreshed() {
    await onChanged();
    // The editor is rebased on the deal as the next read returns it.
    setOpened(null);
    setRevision((value) => value + 1);
    setReason("");
  }

  async function act(action: "submissions" | "cancellations", message: string) {
    const saved = await actions.save(`${dealPath(deal.id)}/${action}`, "POST", {
      expectedVersion: deal.version,
      reason: action === "cancellations" ? reason : null,
    });
    if (saved) {
      toast.success(message);
      await refreshed();
    }
  }

  async function release() {
    const saved = await actions.save(`${holdsPath(deal.reservationId!)}/releases`, "POST", {});
    if (saved) {
      toast.success("Claim released. The hold is back in the queue with the time it had left.");
      onReleased();
    }
  }

  return (
    <>
      <LoadingProgress label="Saving deal" active={busy} />
      <div className={styles.body}>
        <section className={workbench.stack}>
          <h3>Price</h3>
          <DealLines deal={deal} />
          {deal.discountLimitAtSubmit !== null && (
            <p className={workbench.help}>
              Agent discount limit when submitted: {formatCurrency(deal.discountLimitAtSubmit)}.
              {deal.negotiatedDiscount > deal.discountLimitAtSubmit &&
                ` The negotiated discount is ${formatCurrency(
                  deal.negotiatedDiscount - deal.discountLimitAtSubmit,
                )} over it.`}
            </p>
          )}
        </section>
        {newer && (
          <div className={workbench.protocol} role="status">
            <p>A newer version was loaded. Your edits are kept until you use it.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => {
                setOpened(deal);
                setRevision((value) => value + 1);
              }}
            >
              Use latest values
            </Button>
          </div>
        )}
        {can.edit && base && (
          <div className={forms.stackTop}>
            <DealTermsForm
              key={`${base.id}:${revision}`}
              base={base}
              saver={terms}
              disabled={busy}
              onSaved={() => void refreshed()}
              onReload={onReload}
              onDirty={setDirty}
            />
          </div>
        )}
        {can.cancel && (
          <section className={workbench.stack}>
            <h3>Reason to cancel</h3>
            <Textarea
              label="Reason"
              labelHidden
              placeholder="Required to cancel this deal"
              maxLength={1000}
              rows={3}
              value={reason}
              disabled={busy}
              onChange={(event) => setReason(event.target.value.replace(/^\s+/, ""))}
            />
          </section>
        )}
        {actions.notice && (
          <div className={workbench.protocol} role="alert">
            <p>
              {actions.notice.status === 409
                ? "This deal changed elsewhere. Refresh it before acting again."
                : actions.notice.message}
            </p>
          </div>
        )}
      </div>
      <StaffWorkspaceFooter
        title={dealStatusLabels[deal.status]}
        description={footerNote(deal, dirty)}
      >
        {can.submit && (
          <ConfirmationButton
            confirmationTitle="Submit this deal?"
            message="Submitting captures today's fee, tax and discount limit. A discount within your limit is approved at once; a larger one goes to a manager."
            size="compact"
            disabled={busy || dirty}
            onConfirm={() => act("submissions", "Deal submitted.")}
          >
            Submit deal
          </ConfirmationButton>
        )}
        {releasable && (
          <ConfirmationButton
            confirmationTitle="Release your claim?"
            message="The draft deal is cancelled and the hold returns to the queue with the time it had left."
            size="compact"
            variant="secondary"
            disabled={busy}
            onConfirm={release}
          >
            Release claim
          </ConfirmationButton>
        )}
        {can.cancel && (
          <ConfirmationButton
            confirmationTitle="Cancel this deal?"
            message="This is final. The deposit is refunded and the vehicle becomes available again."
            size="compact"
            variant="danger"
            disabled={busy || !reasonOk}
            onConfirm={() => act("cancellations", "Deal cancelled.")}
          >
            Cancel deal
          </ConfirmationButton>
        )}
      </StaffWorkspaceFooter>
    </>
  );
}
