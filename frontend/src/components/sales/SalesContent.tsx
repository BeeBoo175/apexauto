"use client";

import { DealFacts } from "@/components/deals/DealFacts";
import { WorkshopLoadingShell } from "@/components/inspection/WorkshopLoadingShell";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import type { useStaffSelection } from "@/hooks/use-staff-selection";
import type { CurrentActor } from "@/lib/auth/actor";
import { dealStatusLabels, type Deal } from "@/lib/deals/deals";
import { salesSectionLabels, salesSections, type SalesSection } from "@/lib/sales/sales";
import { HoldWheel } from "./HoldWheel";
import { SalesDealSection } from "./SalesDealSection";
import { SalesHistory } from "./SalesHistory";
import styles from "@/components/inspection/TechnicianWorkspace.module.css";
import sales from "./Sales.module.css";

// The selected deal beside the queue: header, then Customer & car, Deal and History as separate sections.
export function SalesContent({
  actor,
  selection,
  section,
  now,
  onSection,
  onChanged,
  onReleased,
}: {
  actor: CurrentActor;
  selection: ReturnType<typeof useStaffSelection<Deal>>;
  section: SalesSection;
  now: number;
  onSection: (section: SalesSection) => void;
  onChanged: () => Promise<void>;
  onReleased: () => void;
}) {
  const deal = selection.result?.data ?? null;
  if (!selection.id && !selection.pendingId) {
    return (
      <section className={styles.placeholder} aria-label="Deal content">
        <h2>Select a hold</h2>
        <p>Choose a deal you are handling, or claim an open hold to start one.</p>
      </section>
    );
  }
  if (selection.busy && !deal) {
    return (
      <section className={styles.content} aria-label="Deal content" aria-busy="true">
        <WorkshopLoadingShell />
      </section>
    );
  }
  return (
    <section
      className={styles.content}
      aria-label="Selected deal"
      aria-busy={selection.busy}
      inert={selection.busy}
    >
      <header className={styles.contentHeader}>
        <div>
          <h2>{deal?.context.registration ?? "Deal"}</h2>
          {deal && (
            <p>
              {deal.context.vehicle} · {deal.context.customer}
            </p>
          )}
        </div>
        {deal && (
          <Badge variant={deal.status === "Cancelled" ? "neutral" : "info"}>
            {dealStatusLabels[deal.status]}
          </Badge>
        )}
      </header>
      {deal && (
        <>
          <nav className={styles.queueNavigation} aria-label="Deal sections">
            {salesSections.map((item) => (
              <Button
                key={item}
                size="compact"
                variant="ghost"
                className={styles.queueView}
                aria-pressed={section === item}
                onClick={() => onSection(item)}
              >
                {salesSectionLabels[item]}
              </Button>
            ))}
          </nav>
          {section === "customer" && (
            <div className={sales.body}>
              {deal.status !== "Cancelled" && deal.context.holdCreatedAtUtc && (
                <div className={sales.holdSummary}>
                  <strong>Hold</strong>
                  <HoldWheel
                    expiresAtUtc={deal.context.holdExpiresAtUtc}
                    frozenRemainingSeconds={deal.context.holdFrozenRemainingSeconds}
                    now={now}
                  />
                </div>
              )}
              <DealFacts deal={deal} />
            </div>
          )}
          {section === "deal" && (
            <SalesDealSection
              key={deal.id}
              deal={deal}
              actor={actor}
              onChanged={onChanged}
              onReleased={onReleased}
              onReload={() => void selection.choose(selection.id)}
            />
          )}
          {section === "history" && (
            <div className={sales.body}>
              {deal.reservationId ? (
                <SalesHistory reservationId={deal.reservationId} />
              ) : (
                <p>This deal was not started from a hold, so it has no hold history.</p>
              )}
            </div>
          )}
        </>
      )}
      {!selection.busy && !deal && (
        <div className={styles.empty}>
          <p>This deal is unavailable. Your queues remain retained.</p>
          <Button
            size="compact"
            variant="secondary"
            onClick={() => void selection.choose(selection.id)}
          >
            Retry deal
          </Button>
        </div>
      )}
    </section>
  );
}
