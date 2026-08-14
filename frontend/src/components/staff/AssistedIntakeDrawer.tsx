"use client";

import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useSellerOptions } from "@/hooks/use-seller-options";
import type { CurrentActor } from "@/lib/auth/actor";
import { staffSellNotice } from "@/lib/intake/staff-sell-notice";
import { AssistedIntakeForm } from "./AssistedIntakeForm";
import { StaffDrawerLoadingShell } from "./StaffDrawerLoadingShell";
import workbench from "./StaffWorkspace.module.css";

// Sales staff book a trade-in appointment for an existing customer without leaving the workspace, on one plain
// form like the other drawers. The customer follows the stepped journey on the public page instead.
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
  const loading = !options && !failed;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="New intake for a customer"
      description="Book a trade-in inspection for an existing customer."
      variant="drawer"
      density="compact"
      headerLoading={loading}
    >
      {loading && <StaffDrawerLoadingShell label="Loading vehicle choices…" single tabs={false} />}
      {failed && (
        <div className={workbench.protocol}>
          <p>The vehicle choices could not be loaded.</p>
          <Button size="compact" variant="secondary" onClick={retry}>
            Retry
          </Button>
        </div>
      )}
      {options && notice && (
        <AssistedIntakeForm
          initial={{
            taxonomy: options.taxonomy,
            branches: options.branches,
            account: null,
            access: 200,
            staffNotice: notice,
          }}
          ready
          onBooked={onBooked}
          onClose={onClose}
        />
      )}
    </Dialog>
  );
}
