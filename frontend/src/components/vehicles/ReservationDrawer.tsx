"use client";

import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { RefreshButton } from "@/components/ui/RefreshButton";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { WorkshopLoadingShell } from "@/components/inspection/WorkshopLoadingShell";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import type { ReservationDetail } from "@/lib/vehicles/reservation-detail";
import { reservationStatuses } from "@/lib/vehicles/reservations";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ReservationDrawer({
  selection,
}: {
  selection: ReturnType<typeof useStaffSelection<ReservationDetail>>;
}) {
  const detail = selection.result?.data;
  const reservation = detail?.reservation;
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
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        layout="workspace"
        title="Reservation details"
        description={reservation ? `${reservation.vehicle} · ${reservation.branchName}` : undefined}
        onClose={selection.close}
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
                  <strong>{reservationStatuses[reservation.status]}</strong>
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
                {detail.release && (
                  <section className={workbench.stack}>
                    <h3>Simulated refund</h3>
                    <dl className={workbench.details}>
                      <div>
                        <dt>Refund reference</dt>
                        <dd>{detail.release.id}</dd>
                      </div>
                      <div>
                        <dt>Refunded deposit</dt>
                        <dd>{formatCurrency(detail.release.refund)}</dd>
                      </div>
                      <div>
                        <dt>Recorded</dt>
                        <dd>{formatMalaysianDateTime(detail.release.recordedAt)}</dd>
                      </div>
                      {detail.release.reason && (
                        <div>
                          <dt>Reason</dt>
                          <dd>{detail.release.reason}</dd>
                        </div>
                      )}
                    </dl>
                  </section>
                )}
              </section>
            </div>
            <footer className={workshop.footer}>
              <div className={workshop.footerContext}>
                <strong>Simulated portfolio reservation</strong>
                <span>No real payment or refund is processed.</span>
              </div>
              <Button size="compact" variant="secondary" onClick={selection.close}>
                Close details
              </Button>
            </footer>
          </div>
        )}
      </Dialog>
    </>
  );
}
