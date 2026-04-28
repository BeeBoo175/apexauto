"use client";

import { useState } from "react";
import { ConfirmationDialog } from "@/components/ui/ConfirmationDialog";
import type { useSalesQueue } from "@/hooks/use-sales-queue";
import { useSettingsSave } from "@/hooks/use-settings-save";
import { parseDeal, type Deal } from "@/lib/deals/deals";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { holdsPath, type SalesQueueItem } from "@/lib/sales/sales";
import { HoldWheel } from "./HoldWheel";
import { SalesQueueList } from "./SalesQueueList";
import styles from "@/components/inspection/TechnicianWorkspace.module.css";

// Open holds to claim: choosing one asks for confirmation, because claiming starts the deal and pauses the hold's timer.
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
  onClaimed: (deal: Deal) => void;
  onConflict: () => void;
}) {
  const [hold, setHold] = useState<SalesQueueItem | null>(null);
  // One identity per confirmation lets a lost response be retried without a second deal.
  const [operation, setOperation] = useState("");
  const claim = useSettingsSave(parseDeal);

  function choose(item: SalesQueueItem) {
    claim.clearNotice();
    setOperation(crypto.randomUUID());
    setHold(item);
  }

  async function confirm() {
    if (!hold) {
      return;
    }
    const deal = await claim.save(`${holdsPath(hold.reservationId)}/claims`, "POST", {
      id: operation,
    });
    if (deal) {
      setHold(null);
      onClaimed(deal);
    } else if (claim.notice?.status === 409 || claim.notice?.status === 404) {
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
        confirmLabel="Claim hold"
        cancelLabel="Not now"
        busy={claim.busy}
        onClose={() => setHold(null)}
        onConfirm={confirm}
        facts={
          hold
            ? [
                { label: "Vehicle", value: hold.vehicle },
                { label: "Customer", value: hold.customer },
                { label: "Reserved", value: formatMalaysianDateTime(hold.holdCreatedAtUtc) },
                {
                  label: "Time left",
                  value: (
                    <HoldWheel
                      expiresAtUtc={hold.expiresAtUtc}
                      frozenRemainingSeconds={hold.frozenRemainingSeconds}
                      now={now}
                    />
                  ),
                },
              ]
            : []
        }
      >
        <p>
          Claiming creates a draft deal for this customer and pauses the hold&apos;s timer. You then
          contact the customer and agree the terms.
        </p>
        {claim.notice && (
          <p role="alert">
            {claim.notice.status === 409 || claim.notice.status === 404
              ? "Another agent may have claimed this hold, or it is no longer open. The queue was refreshed."
              : claim.notice.message}
          </p>
        )}
      </ConfirmationDialog>
    </>
  );
}
