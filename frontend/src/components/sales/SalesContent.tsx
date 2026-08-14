"use client";

import { StaffQueuePlaceholder } from "@/components/staff/StaffQueuePlaceholder";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
import { Button } from "@/components/ui/Button";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import { SaleEditor } from "./SaleEditor";
import styles from "@/components/staff/StaffQueueWorkspace.module.css";
import { CustomerAvatar } from "@/components/staff/CustomerAvatar";

// The selected sale beside the agent's queue: the same editor the managers' drawer hosts.
export function SalesContent({
  selection,
  now,
  actionsContainer,
  onChanged,
}: {
  selection: ReturnType<typeof useSaleSelection>;
  now: number;
  actionsContainer: HTMLElement | null;
  onChanged: () => void;
}) {
  const sale = selection.result?.data ?? null;
  if (!selection.id && !selection.pendingId) {
    return (
      <StaffQueuePlaceholder label="Sale content" title="Select a sale">
        Choose a sale you are handling, or claim an open hold to start one.
      </StaffQueuePlaceholder>
    );
  }
  if (selection.busy && !sale) {
    return (
      <section className={styles.content} aria-label="Sale content" aria-busy="true">
        <StaffDrawerLoadingShell label="Loading sale…" fact />
      </section>
    );
  }
  return (
    <section
      className={styles.content}
      aria-label="Selected sale"
      aria-busy={selection.busy}
      inert={selection.busy}
    >
      <header className={styles.contentHeader}>
        <div>
          <h2>{sale?.context.registration ?? "Sale"}</h2>
          {sale && (
            <p>
              {sale.context.vehicle} ·{" "}
              <CustomerAvatar id={sale.context.customerId} name={sale.context.customer} />
            </p>
          )}
        </div>
      </header>
      {sale && (
        <div className={styles.editor}>
          <SaleEditor
            key={sale.id}
            sale={sale}
            selection={selection}
            now={now}
            actionsContainer={actionsContainer}
            scrollWithHost
            onChanged={onChanged}
            onLocked={selection.lock}
          />
        </div>
      )}
      {!selection.busy && !sale && (
        <div className={styles.empty}>
          <p>This sale is unavailable. Your queues remain retained.</p>
          <Button
            size="compact"
            variant="secondary"
            onClick={() => void selection.choose(selection.id)}
          >
            Retry sale
          </Button>
        </div>
      )}
    </section>
  );
}
