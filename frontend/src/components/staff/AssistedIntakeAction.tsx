"use client";

import { useContext } from "react";
import { hasIntakeAccess } from "@/lib/auth/actor";
import { StaffActorContext } from "./StaffActorBoundary";
import { AssistedIntakeDrawer } from "./AssistedIntakeDrawer";
import { AssistedStartButton } from "./AssistedStartButton";

// The create control of an intake list or queue. The workspace's history owns whether the drawer is open, as it
// does for a selected record, so the address, Back and a reload all agree with what is shown.
export function AssistedIntakeAction({
  open,
  onOpen,
  onClose,
  disabled = false,
  onBooked,
}: {
  open: boolean;
  onOpen: () => void;
  onClose: () => void;
  disabled?: boolean;
  onBooked: () => void;
}) {
  const actor = useContext(StaffActorContext);
  if (!actor || !hasIntakeAccess(actor)) {
    return null;
  }

  return (
    <>
      <AssistedStartButton label="New intake for a customer" disabled={disabled} onClick={onOpen} />
      <AssistedIntakeDrawer actor={actor} open={open} onClose={onClose} onBooked={onBooked} />
    </>
  );
}
