"use client";

import { useState } from "react";
import { StaffDrawerLoadingShell } from "@/components/staff/StaffDrawerLoadingShell";
import { Button } from "@/components/ui/Button";
import { Dialog, DialogActionSlot } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import type { useSaleSelection } from "@/hooks/use-sale-selection";
import { SaleEditor } from "./SaleEditor";
import workbench from "@/components/staff/StaffWorkspace.module.css";

// The managers' drawer over the Sales table, hosting the same sale editor the agents use, as the workshop drawer does.
export function SaleDrawer({
  selection,
  now,
  onChanged,
}: {
  selection: ReturnType<typeof useSaleSelection>;
  now: number;
  onChanged: () => void;
}) {
  const [headerActions, setHeaderActions] = useState<HTMLDivElement | null>(null);
  const sale = selection.result?.data ?? null;
  const loading = selection.busy && !sale;
  return (
    <>
      <LoadingProgress label="Opening sale" active={selection.busy} />
      <Dialog
        open={Boolean(selection.id || selection.pendingId)}
        variant="drawer"
        density="compact"
        width="wide"
        title={sale ? (sale.sale.orderNumber ?? sale.context.reservationNumber) : "Sale"}
        headerLoading={loading}
        description={sale ? `${sale.context.registration} · ${sale.context.vehicle}` : undefined}
        headerActions={<DialogActionSlot ref={setHeaderActions} />}
        onClose={selection.close}
      >
        {loading && <StaffDrawerLoadingShell label="Loading sale…" />}
        {!selection.busy && !sale && selection.id && (
          <div className={workbench.protocol}>
            <p>The sale is unavailable. Your table remains unchanged.</p>
            <Button
              size="compact"
              variant="secondary"
              onClick={() => void selection.choose(selection.id)}
            >
              Retry sale
            </Button>
          </div>
        )}
        {sale && (
          <SaleEditor
            key={sale.id}
            sale={sale}
            selection={selection}
            now={now}
            actionsContainer={headerActions}
            onChanged={onChanged}
            onLocked={selection.lock}
          />
        )}
      </Dialog>
    </>
  );
}
