"use client";

import { StaffQueuePlaceholder } from "@/components/staff/StaffQueuePlaceholder";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
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
  onGoToClaim,
}: {
  selection: ReturnType<typeof useSaleSelection>;
  now: number;
  actionsContainer: HTMLElement | null;
  onChanged: () => void;
  // Opens the To claim tab, which the empty content pane points to.
  onGoToClaim: () => void;
}) {
  const sale = selection.result?.data ?? null;
  if (!selection.id && !selection.pendingId) {
    return (
      <StaffQueuePlaceholder
        label="Sale content"
        title="Select a sale"
        link={{ text: "To claim", onClick: onGoToClaim }}
      >
        Open one from your queue, or claim a hold in To claim.
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
          <ReloadNotice subject="Sale" />
        </div>
      )}
    </section>
  );
}
