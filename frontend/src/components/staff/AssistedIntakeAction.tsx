"use client";

import { useContext, useState } from "react";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { StaffActorContext } from "./StaffActorBoundary";
import { AssistedIntakeDrawer } from "./AssistedIntakeDrawer";
import { AssistedStartButton } from "./AssistedStartButton";

// The create control of an intake list or queue, which owns the drawer it opens.
export function AssistedIntakeAction({
  disabled = false,
  onBooked,
}: {
  disabled?: boolean;
  onBooked: () => void;
}) {
  const actor = useContext(StaffActorContext);
  const [open, setOpen] = useState(false);
  if (!actor || !hasIntakeAccess(actor)) {
    return null;
  }

  return (
    <>
      <AssistedStartButton
        label="New intake for a customer"
        disabled={disabled}
        onClick={() => setOpen(true)}
      />
      <AssistedIntakeDrawer
        actor={actor}
        open={open}
        onClose={() => setOpen(false)}
        onBooked={onBooked}
      />
    </>
  );
}
