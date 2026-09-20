"use client";

import type { KeyboardEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Textarea } from "@/components/ui/Input";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { useRetentionHolds } from "@/hooks/use-retention-holds";
import { formatMalaysianDateTime } from "@/lib/date-format";
import styles from "@/components/staff/StaffForms.module.css";

// Controls are not a form: the surrounding retention form owns Enter, so no field here may submit it.
function keepEnterFromSettings(event: KeyboardEvent<HTMLElement>) {
  if (event.key === "Enter" && event.target instanceof HTMLInputElement) {
    event.preventDefault();
  }
}

export function RetentionHolds() {
  const holds = useRetentionHolds();
  return (
    <section
      className={styles.section}
      aria-label="Preservation holds"
      onKeyDown={keepEnterFromSettings}
    >
      <h3>Preservation holds</h3>
      <p className={styles.hint}>
        A held sale keeps its buyer details, documents and photographs past the business evidence
        period until the hold date. Name the sale by its reservation number.
      </p>
      <div className={styles.pair}>
        <Input
          label="Reservation number"
          density="compact"
          value={holds.draft.reservationNumber}
          disabled={holds.busy}
          maxLength={40}
          onChange={(event) => holds.edit({ reservationNumber: event.target.value })}
        />
        <Input
          label="Hold until"
          density="compact"
          type="date"
          value={holds.draft.until}
          disabled={holds.busy}
          onChange={(event) => holds.edit({ until: event.target.value })}
        />
      </div>
      <Textarea
        label="Reason"
        rows={2}
        maxLength={1000}
        value={holds.draft.reason}
        disabled={holds.busy}
        onChange={(event) => holds.edit({ reason: event.target.value })}
      />
      <div>
        <Button
          type="button"
          size="compact"
          disabled={holds.busy || !holds.valid}
          onClick={() => void holds.place()}
        >
          Place hold
        </Button>
      </div>
      {holds.failed && <ReloadNotice subject="Preservation holds" />}
      {holds.holds && holds.holds.length === 0 && <p className={styles.hint}>No sale is held.</p>}
      {holds.holds && holds.holds.length > 0 && (
        <ul aria-label="Current holds">
          {holds.holds.map((hold) => (
            <li key={hold.saleId}>
              <strong>{hold.reservationNumber}</strong> until {formatMalaysianDateTime(hold.until)}.{" "}
              {hold.reason}{" "}
              <Button
                type="button"
                size="compact"
                variant="secondary"
                disabled={holds.busy}
                onClick={() => void holds.release(hold)}
              >
                Release {hold.reservationNumber}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
