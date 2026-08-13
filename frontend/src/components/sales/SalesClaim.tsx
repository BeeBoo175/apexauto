"use client";

import { useState } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import { useApiCommand } from "@/hooks/use-api-command";
import { useEditGuard } from "@/hooks/use-edit-guard";
import type { useSalesQueue } from "@/hooks/use-sales-queue";
import { privateApiFetch } from "@/lib/api-browser";
import { isRecord } from "@/lib/auth/contracts";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { parseSaleReceipt } from "@/lib/sales/parse";
import { salePath } from "@/lib/sales/query";
import type { SaleSummary } from "@/lib/sales/contracts";
import { HoldWheel } from "./HoldWheel";
import { SalesQueueList } from "./SalesQueueList";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import { CustomerAvatar } from "@/components/staff/CustomerAvatar";

// Open holds to claim: choosing one asks for confirmation, because claiming makes the agent its handler and pauses
// the hold's timer. A lost response replays the same operation rather than claiming twice.
export function SalesClaim({
  state,
  now,
  selectionLocked,
  onClaimed,
  onConflict,
}: {
  state: ReturnType<typeof useSalesQueue>;
  now: number;
  selectionLocked: boolean;
  onClaimed: (saleId: string) => void;
  onConflict: () => void;
}) {
  const [hold, setHold] = useState<SaleSummary | null>(null);
  const [notice, setNotice] = useState("");
  const claim = useApiCommand(
    hold ? `${salePath(hold.id)}/claims` : "",
    parseSaleReceipt,
    (receipt, request) =>
      isRecord(request) &&
      receipt.operationId === request.operationId &&
      receipt.saleId === hold?.id,
    privateApiFetch,
  );
  useEditGuard({ pending: claim.busy, uncertain: claim.uncertain, label: "Sale claim" });

  function choose(item: SaleSummary) {
    if (claim.uncertain) {
      return;
    }
    setNotice("");
    setHold(item);
  }

  async function confirm() {
    if (!hold) {
      return;
    }
    const result = await claim.submit({
      operationId: crypto.randomUUID(),
      expectedVersion: hold.version,
    });
    if (result?.status === 200 && result.data) {
      setHold(null);
      onClaimed(result.data.saleId);
      return;
    }
    if (result?.status === 409 || result?.status === 404) {
      setNotice(
        "Another agent may have claimed this hold, or it is no longer open. The queue was refreshed.",
      );
      onConflict();
    }
  }

  return (
    <>
      <div className={styles.queueList} inert={Boolean(hold)}>
        <SalesQueueList
          view="ToClaim"
          title="Holds to claim"
          state={state}
          now={now}
          selectionLocked={selectionLocked}
          onOpen={choose}
        />
      </div>
      <ConfirmationDialog
        open={Boolean(hold)}
        title={`Claim hold · ${hold?.registration ?? ""}`}
        confirmLabel={claim.uncertain ? "Reconcile claim" : "Claim hold"}
        cancelLabel="Not now"
        busy={claim.busy}
        locked={claim.uncertain}
        onClose={() => setHold(null)}
        onConfirm={confirm}
        facts={
          hold
            ? [
                { label: "Vehicle", value: `${hold.manufactureYear} ${hold.make} ${hold.model}` },
                {
                  label: "Customer",
                  value: <CustomerAvatar id={hold.customerId} name={hold.customer} />,
                },
                { label: "Reservation", value: hold.reservationNumber },
                { label: "Expires", value: formatMalaysianDateTime(hold.dueAt ?? "") },
                {
                  label: "Time left",
                  value: (
                    <HoldWheel dueAt={hold.dueAt} pausedSeconds={hold.pausedSeconds} now={now} />
                  ),
                },
              ]
            : []
        }
      >
        <p>
          Claiming makes you the agent for this sale and pauses the hold&apos;s timer. You then
          contact the customer and agree the terms.
        </p>
        {claim.uncertain && (
          <p role="alert">The claim is unconfirmed. Confirm again to reconcile it.</p>
        )}
        {notice && <p role="alert">{notice}</p>}
      </ConfirmationDialog>
    </>
  );
}
