"use client";

import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { ReservationLoadingShell } from "./ReservationLoadingShell";
import { ReservationHandling } from "./ReservationHandling";
import { PersonIdentityFacts, VehicleIdentityFacts } from "@/components/staff/IdentityFacts";
import { SalesHistory } from "@/components/sales/SalesHistory";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import { useReservationCancellation } from "@/hooks/use-reservation-cancellation";
import type { CurrentActor } from "@/lib/auth/actor";
import { reservationCancellationReason } from "@/lib/vehicles/reservation-cancellation";
import type { ReservationDetail } from "@/lib/vehicles/reservation-detail";
import { reservationStatuses } from "@/lib/vehicles/reservations";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "./ReservationDrawer.module.css";

export function ReservationDrawer({
  selection,
  actor,
  now,
  onConfirmed,
}: {
  selection: ReturnType<typeof useStaffSelection<ReservationDetail>>;
  actor: CurrentActor;
  now: number;
  onConfirmed: () => void;
}) {
  const detail = selection.result?.data;
  const reservation = detail?.reservation;
  const cancellation = useReservationCancellation(detail ?? null, actor, onConfirmed);
  const release = cancellation.command.receipt ?? detail?.release;
  const status = cancellation.command.receipt ? "StaffCancelled" : reservation?.status;
  const statusLabel = reservation?.claim ? "Being handled" : status && reservationStatuses[status];
  const handleable = status === "Active" && !cancellation.command.receipt;
  const close = () => {
    if (!cancellation.locked) {
      selection.close();
    }
  };
  const facts =
    detail && reservation
      ? [
          ["Reference", reservation.number],
          ["Created", formatMalaysianDateTime(reservation.createdAt)],
          ["Expires", formatMalaysianDateTime(reservation.expiresAt)],
          ["Simulated deposit", formatCurrency(detail.deposit)],
          ["Accepted promo", detail.promoCode ?? "None"],
        ]
      : [];

  return (
    <>
      <LoadingProgress label="Loading reservation details" active={selection.busy} />
      <LoadingProgress label="Cancelling reservation" active={cancellation.command.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title="Reservation details"
        headerLoading={selection.busy && !detail}
        description={reservation ? `${reservation.vehicle} · ${reservation.branchName}` : undefined}
        onClose={close}
        closeDisabled={cancellation.locked}
        headerActions={
          <RefreshButton
            label="Refresh reservation details"
            busy={selection.busy}
            disabled={!selection.id}
            onClick={() => void selection.choose(selection.id)}
          />
        }
      >
        {selection.busy && !detail && <ReservationLoadingShell />}
        {!selection.busy && !detail && (
          <div className={workbench.protocol}>
            <p>The reservation is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry reservation
            </Button>
          </div>
        )}
        {detail && reservation && (
          <div className={workshop.workspace}>
            <div className={workshop.summary}>
              <div className={workshop.summaryMain}>
                <div>
                  <span className={workshop.summaryLabel}>Reserved total</span>
                  <strong className={workshop.summaryValue}>
                    {formatCurrency(reservation.total)}
                  </strong>
                </div>
                <div className={workshop.summaryContext}>
                  <strong>{statusLabel}</strong>
                  <span>{reservation.branchName}</span>
                </div>
              </div>
            </div>
            <div className={`${workshop.body} ${styles.body}`}>
              <section className={`${workbench.stack} ${styles.section}`}>
                <h3>Confirmed reservation</h3>
                <dl className={workbench.details}>
                  <VehicleIdentityFacts
                    registration={reservation.registrationPlate}
                    vehicle={reservation.vehicle}
                    vin={detail.vin}
                  />
                  <PersonIdentityFacts
                    role="Buyer"
                    name={reservation.buyer}
                    mobile={detail.contact.mobile}
                    email={detail.contact.email}
                  />
                  {facts.map(([label, value]) => (
                    <div key={label}>
                      <dt>{label}</dt>
                      <dd>{value}</dd>
                    </div>
                  ))}
                </dl>
              </section>
              {handleable && (
                <ReservationHandling detail={detail} now={now} onChanged={onConfirmed} />
              )}
              <section className={`${workbench.stack} ${styles.section}`}>
                <h3>Retained price</h3>
                <dl className={workbench.details}>
                  {[
                    ["Vehicle price", detail.price.vehiclePrice],
                    ["Processing fee", detail.price.processingFee],
                    ["Road tax", detail.price.roadTax],
                    ["Service tax", detail.price.serviceTax],
                    ["Promo discount", detail.price.promoDiscount],
                    ["Reserved total", detail.price.total],
                  ].map(([label, value]) => (
                    <div key={label}>
                      <dt>{label}</dt>
                      <dd>{formatCurrency(Number(value))}</dd>
                    </div>
                  ))}
                </dl>
                <p className={workbench.help}>
                  These are the confirmed reservation terms. Later listing edits do not change this
                  price. Insurance and optional warranty are excluded.
                </p>
              </section>
              {release && (
                <section className={`${workbench.stack} ${styles.section}`}>
                  <h3>Simulated refund</h3>
                  <dl className={workbench.details}>
                    <div>
                      <dt>Refunded deposit</dt>
                      <dd>{formatCurrency(release.refund)}</dd>
                    </div>
                    <div>
                      <dt>Recorded</dt>
                      <dd>{formatMalaysianDateTime(release.recordedAt)}</dd>
                    </div>
                    {release.reason && (
                      <div>
                        <dt>Reason</dt>
                        <dd>{release.reason}</dd>
                      </div>
                    )}
                  </dl>
                </section>
              )}
              <SalesHistory
                key={`${reservation.id}:${status}:${reservation.claim?.agent ?? ""}`}
                reservationId={reservation.id}
              />
              {(cancellation.actionable || cancellation.locked) && (
                <section className={`${workbench.stack} ${styles.section} ${styles.cancellation}`}>
                  <div className={styles.cancellationIntro}>
                    <h3>Cancel reservation</h3>
                    <p>
                      Cancellation releases this hold and records a full simulated deposit refund.
                    </p>
                  </div>
                  <Textarea
                    label="Cancellation reason"
                    labelHidden
                    placeholder="Explain why this reservation is being cancelled"
                    required
                    maxLength={1000}
                    rows={3}
                    value={cancellation.reason}
                    disabled={cancellation.operating || !cancellation.actionable}
                    aria-invalid={
                      cancellation.validationAttempted &&
                      !reservationCancellationReason(cancellation.reason)
                    }
                    onChange={(event) => cancellation.changeReason(event.target.value)}
                  />
                  {cancellation.command.uncertain && (
                    <p>
                      The cancellation is unconfirmed. Reconcile the same request before leaving.
                      Refreshed details do not confirm this operation.
                    </p>
                  )}
                  {cancellation.reason && (
                    <ConfirmationButton
                      confirmationTitle="Discard cancellation reason?"
                      message="Your unsaved cancellation reason will be cleared. The reservation will remain unchanged."
                      size="compact"
                      variant="secondary"
                      disabled={cancellation.operating}
                      onConfirm={cancellation.discard}
                    >
                      Discard cancellation reason
                    </ConfirmationButton>
                  )}
                </section>
              )}
            </div>
            <footer className={workbench.footer}>
              <div className={workbench.footerContext}>
                <strong>Simulated portfolio reservation</strong>
                <span>No real payment or refund is processed.</span>
              </div>
              {(cancellation.actionable || cancellation.command.uncertain) && (
                <ConfirmationButton
                  confirmationTitle="Cancel this reservation?"
                  className={styles.cancelAction}
                  message={`Release the hold for ${reservation.vehicle} and record a full simulated deposit refund of ${formatCurrency(detail.deposit)}. This cannot be undone.`}
                  skipConfirmation={cancellation.command.uncertain}
                  size="compact"
                  variant="danger"
                  disabled={cancellation.command.busy}
                  aria-busy={cancellation.command.busy}
                  onConfirm={() => cancellation.cancel()}
                >
                  {cancellation.command.uncertain
                    ? "Reconcile reservation cancellation"
                    : "Cancel reservation"}
                </ConfirmationButton>
              )}
            </footer>
          </div>
        )}
      </Dialog>
    </>
  );
}
