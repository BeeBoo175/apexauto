"use client";

import { Button } from "@/components/ui/Button";
import { Textarea } from "@/components/ui/Input";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { WorkshopLoadingShell } from "@/components/inspection/WorkshopLoadingShell";
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

export function ReservationDrawer({
  selection,
  actor,
  onConfirmed,
}: {
  selection: ReturnType<typeof useStaffSelection<ReservationDetail>>;
  actor: CurrentActor;
  onConfirmed: () => void;
}) {
  const detail = selection.result?.data;
  const reservation = detail?.reservation;
  const cancellation = useReservationCancellation(detail ?? null, actor, onConfirmed);
  const release = cancellation.command.receipt ?? detail?.release;
  const status = cancellation.command.receipt ? "StaffCancelled" : reservation?.status;
  const close = () => {
    if (!cancellation.locked) {
      selection.close();
    }
  };
  const facts =
    detail && reservation
      ? [
          ["Reference", reservation.id],
          ["Buyer", reservation.buyer],
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
        {selection.busy && !detail && <WorkshopLoadingShell label="Loading reservation…" />}
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
                  <strong>{status && reservationStatuses[status]}</strong>
                  <span>{reservation.branchName}</span>
                </div>
              </div>
            </div>
            <div className={workshop.body}>
              <section className={workbench.stack}>
                <h3>Confirmed reservation</h3>
                <dl className={workbench.details}>
                  {facts.map(([label, value]) => (
                    <div key={label}>
                      <dt>{label}</dt>
                      <dd>{value}</dd>
                    </div>
                  ))}
                </dl>
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
                {release && (
                  <section className={workbench.stack}>
                    <h3>Simulated refund</h3>
                    <dl className={workbench.details}>
                      <div>
                        <dt>Refund reference</dt>
                        <dd>{release.id}</dd>
                      </div>
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
                {(cancellation.actionable || cancellation.locked) && (
                  <section className={workbench.stack}>
                    <h3>Cancel reservation</h3>
                    <p>
                      Cancellation releases this hold and records a full simulated deposit refund.
                    </p>
                    <Textarea
                      label="Cancellation reason"
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
                      <Button
                        size="compact"
                        variant="secondary"
                        disabled={cancellation.operating}
                        onClick={cancellation.discard}
                      >
                        Discard cancellation reason
                      </Button>
                    )}
                  </section>
                )}
              </section>
            </div>
            <footer className={workbench.footer}>
              <div className={workbench.footerContext}>
                <strong>Simulated portfolio reservation</strong>
                <span>No real payment or refund is processed.</span>
              </div>
              {(cancellation.actionable || cancellation.command.uncertain) && (
                <Button
                  size="compact"
                  variant="danger"
                  disabled={cancellation.command.busy}
                  aria-busy={cancellation.command.busy}
                  onClick={() => void cancellation.cancel()}
                >
                  {cancellation.command.uncertain
                    ? "Reconcile reservation cancellation"
                    : "Cancel reservation"}
                </Button>
              )}
              <Button
                size="compact"
                variant="secondary"
                disabled={cancellation.locked}
                onClick={close}
              >
                Close details
              </Button>
            </footer>
          </div>
        )}
      </Dialog>
    </>
  );
}
