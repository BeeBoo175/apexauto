"use client";

import { SellerJourney } from "@/components/sell/SellerJourney";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useSellerOptions } from "@/hooks/use-seller-options";
import type { CurrentActor } from "@/lib/auth/actor";
import { staffSellNotice } from "@/lib/intake/staff-sell-notice";
import styles from "./AssistedDialogs.module.css";

// Sales staff book a trade-in appointment for an existing customer without leaving the workspace: the same journey
// the customer follows, in a drawer, with the customer chosen on the photos step.
export function AssistedIntakeDrawer({
  actor,
  open,
  onClose,
  onBooked,
}: {
  actor: CurrentActor;
  open: boolean;
  onClose: () => void;
  onBooked: () => void;
}) {
  const { options, failed, retry } = useSellerOptions(open);
  const notice = staffSellNotice(actor);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New intake for a customer"
      description="Book a trade-in inspection for an existing customer. They are emailed the confirmation."
      variant="drawer"
      width="wide"
    >
      <div className={styles.body}>
        {options && notice && (
          <SellerJourney
            embedded
            initial={{
              taxonomy: options.taxonomy,
              branches: options.branches,
              account: null,
              access: 200,
              staffNotice: notice,
            }}
            onDone={() => {
              onBooked();
              onClose();
            }}
          />
        )}
        {!options && !failed && (
          <p className={styles.note} aria-busy="true">
            Loading vehicle choices…
          </p>
        )}
        {failed && (
          <div className={styles.note}>
            <p>The vehicle choices could not be loaded.</p>
            <Button variant="secondary" onClick={retry}>
              Retry
            </Button>
          </div>
        )}
      </div>
    </Dialog>
  );
}
