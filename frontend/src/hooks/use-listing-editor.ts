"use client";

import { useEffect, useState } from "react";
import { useApiCommand } from "./use-api-command";
import { useInspectionLock } from "./use-inspection-lock";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import {
  listingDraft,
  listingDraftContents,
  matchesListingSave,
  parseListingReceipt,
  type ListingDraft,
} from "@/lib/vehicles/listing-draft";

export function useListingEditor(
  snapshot: ListingWorkspace,
  actor: CurrentActor,
  onLock: (locked: boolean) => void,
  onSaved: () => void,
) {
  const [base, setBase] = useState(snapshot.draft);
  const [value, setValue] = useState(() => listingDraft(snapshot.draft));
  const [dirty, setDirty] = useState(false);
  const [validationAttempted, setValidationAttempted] = useState(false);
  const [entry, setEntry] = useState(snapshot);
  const toast = useToast();
  const command = useApiCommand(
    `/api/staff/listings/${snapshot.vehicle.vehicleId}/drafts`,
    parseListingReceipt,
    (receipt, body) =>
      receipt.vehicleId === snapshot.vehicle.vehicleId &&
      receipt.branchId === snapshot.vehicle.branchId &&
      receipt.actorId === actor.userId &&
      matchesListingSave(receipt, body),
    (path, parse, options) => apiFetch(path, parse, options, { requireActor: true }),
  );
  const operating = command.busy || command.uncertain;
  const locked = dirty || operating;
  const lockNavigation = useInspectionLock(locked);
  useEffect(() => {
    onLock(locked);
    return () => onLock(false);
  }, [locked, onLock]);

  if (entry !== snapshot) {
    setEntry(snapshot);
    if (!locked) {
      setBase(snapshot.draft);
      setValue(listingDraft(snapshot.draft));
    }
  }

  const stale = (snapshot.draft?.version ?? 0) > (base?.version ?? 0);

  function change(next: ListingDraft) {
    if (operating) {
      return;
    }
    lockNavigation(true);
    onLock(true);
    setValue(next);
    setDirty(true);
  }

  function discard() {
    if (operating) {
      return;
    }
    const current = stale ? snapshot.draft : base;
    setBase(current);
    setValue(listingDraft(current));
    setDirty(false);
    setValidationAttempted(false);
    lockNavigation(false);
    onLock(false);
  }

  async function save() {
    if (command.busy || (stale && !command.uncertain)) {
      return;
    }
    const contents = listingDraftContents(value);
    if (!contents) {
      setValidationAttempted(true);
      toast.error(
        "Enter exact MYR prices, transmission, fuel and a description of at most 2000 characters.",
      );
      return;
    }
    lockNavigation(true);
    onLock(true);
    const result = await command.submit({
      operationId: crypto.randomUUID(),
      expectedVersion: base?.version ?? 0,
      ...contents,
    });
    if (result?.status === 200 && result.data) {
      setBase(result.data);
      setValue(listingDraft(result.data));
      setDirty(false);
      setValidationAttempted(false);
      lockNavigation(false);
      onLock(false);
      onSaved();
    }
  }

  return {
    base,
    value,
    dirty,
    stale,
    operating,
    locked,
    command,
    validationAttempted,
    change,
    discard,
    save,
  };
}
