"use client";

import { EmptyNotice } from "@/components/ui/EmptyNotice";
import { useAccountCollectionHistory } from "@/hooks/use-account-collection-history";
import type { useAccountReservations } from "@/hooks/use-account-reservations";
import type { ReservationFilters } from "@/lib/vehicles/account-reservations";
import { CollectionHistory } from "./CollectionHistory";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { AccountLoadingShell } from "./AccountLoadingShell";
import { AwaitingCollectionRow } from "./AwaitingCollectionRow";
import styles from "./Account.module.css";
import historyStyles from "./ReservationHistory.module.css";

export function AccountAwaitingCollection({
  history,
  page,
  filters,
  actorId,
  ready,
}: {
  history: ReturnType<typeof useAccountReservations>;
  page: number;
  filters: ReservationFilters;
  actorId: string;
  ready: boolean;
}) {
  const collection = useAccountCollectionHistory(page, filters, actorId);
  // The tab loads and fails as one, like the other account tabs, though it reads two lists.
  const snapshot = history.snapshot;
  const collected = collection.snapshot;
  const loaded = Boolean(snapshot && collected);
  const loading = !loaded && (history.busy || collection.busy);
  const items = snapshot?.awaitingCollection ?? [];
  return (
    <section
      className={`${styles.history} ${historyStyles.historyScope}`}
      aria-labelledby="awaiting-collection-title"
    >
      <div className={styles.heading}>
        <div>
          <h2 id="awaiting-collection-title">Awaiting collection</h2>
          <p className={styles.help}>
            Your paid cars. Contact the showroom to arrange a time before you visit.
          </p>
        </div>
      </div>
      {loading && <AccountLoadingShell label="Loading your cars" />}
      {!loaded && !loading && <ReloadNotice subject="Your cars" />}
      {loaded && items.length === 0 && (
        <EmptyNotice
          icon="car"
          title="No cars awaiting collection"
          action={{ label: "Browse cars", href: "/vehicles", prefetch: false }}
        >
          A car appears here once your full payment is confirmed.
        </EmptyNotice>
      )}
      {loaded && items.length > 0 && (
        <ul className={historyStyles.historyList}>
          {items.map((item) => (
            <AwaitingCollectionRow key={item.receipt.id} item={item} />
          ))}
        </ul>
      )}
      {loaded && collected && (
        <CollectionHistory history={collection} snapshot={collected} ready={ready} />
      )}
    </section>
  );
}
