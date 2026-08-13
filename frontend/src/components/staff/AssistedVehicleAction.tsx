"use client";

import { useContext, useState } from "react";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { StaffActorContext } from "./StaffActorBoundary";
import { AssistedStartButton } from "./AssistedStartButton";
import { AssistedVehicleDialog, type AssistedVehicleKind } from "./AssistedVehicleDialog";

const labels: Record<AssistedVehicleKind, string> = {
  testDrive: "Book a test drive for a customer",
  reservation: "Reserve a car for a customer",
};

// The create control of a test drive or sales list or queue, which owns the dialog it opens. The workspace
// refreshes when the dialog closes, so a booking made for a customer appears in its list.
export function AssistedVehicleAction({
  kind,
  disabled = false,
  onClosed,
}: {
  kind: AssistedVehicleKind;
  disabled?: boolean;
  onClosed: () => void;
}) {
  const actor = useContext(StaffActorContext);
  const [open, setOpen] = useState(false);
  if (!actor || !hasIntakeAccess(actor)) {
    return null;
  }

  return (
    <>
      <AssistedStartButton label={labels[kind]} disabled={disabled} onClick={() => setOpen(true)} />
      <AssistedVehicleDialog
        actor={actor}
        kind={kind}
        open={open}
        onClose={() => {
          setOpen(false);
          onClosed();
        }}
      />
    </>
  );
}
