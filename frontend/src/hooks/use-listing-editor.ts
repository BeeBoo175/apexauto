"use client";

import { useEffect, useState } from "react";
import { useApiCommand } from "./use-api-command";
import { useEditGuard } from "./use-edit-guard";
import { useListingUpload } from "./use-listing-upload";
import { useListingPublication } from "./use-listing-publication";
import { useToast } from "@/components/ui/Toast";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { MAX_LISTING_PHOTOS } from "@/lib/vehicles/listing-photos";
import {
  listingWithdrawalReason,
  matchesListingWithdrawal,
  parseListingWithdrawalReceipt,
} from "@/lib/vehicles/listing-withdrawal";
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
  const [withdrawalReason, setWithdrawalReason] = useState("");
  const [withdrawalTarget, setWithdrawalTarget] = useState<string | null>(null);
  const [withdrawalValidationAttempted, setWithdrawalValidationAttempted] = useState(false);
  const toast = useToast();
  const command = useApiCommand(
    `/api/staff/listings/${snapshot.vehicle.vehicleId}/drafts`,
    parseListingReceipt,
    (receipt, body) =>
      receipt.vehicleId === snapshot.vehicle.vehicleId &&
      receipt.branchId === snapshot.vehicle.branchId &&
      receipt.actorId === actor.userId &&
      matchesListingSave(receipt, body),
    privateApiFetch,
  );
  const upload = useListingUpload(snapshot.vehicle.vehicleId, (id) => {
    if (value.photoIds.length >= MAX_LISTING_PHOTOS) {
      return false;
    }
    setValue((current) => ({ ...current, photoIds: [...current.photoIds, id] }));
    setDirty(true);
    return true;
  });
  const withdrawal = useApiCommand(
    `/api/staff/listings/${snapshot.vehicle.vehicleId}/withdrawals`,
    parseListingWithdrawalReceipt,
    (receipt, body) =>
      receipt.vehicleId === snapshot.vehicle.vehicleId &&
      receipt.branchId === snapshot.vehicle.branchId &&
      receipt.actorId === actor.userId &&
      matchesListingWithdrawal(receipt, body),
    privateApiFetch,
  );
  const stale = (snapshot.draft?.version ?? 0) > (base?.version ?? 0);
  const withdrawnPublicationId =
    snapshot.publication && withdrawal.receipt?.publicationId === snapshot.publication.id
      ? snapshot.publication.id
      : (snapshot.withdrawal?.publicationId ?? null);
  const approval = useListingPublication(snapshot, actor, {
    base,
    ready:
      !dirty &&
      !withdrawalReason &&
      !stale &&
      !upload.file &&
      !command.busy &&
      !command.uncertain &&
      !withdrawal.busy &&
      !withdrawal.uncertain,
    withdrawnPublicationId,
    onPrepared: (receipt) => {
      setBase(receipt);
      setValue(listingDraft(receipt));
      setDirty(false);
      setValidationAttempted(false);
    },
    onSaved,
  });
  const uploading = Boolean(upload.file);
  const approving = approval.busy || approval.uncertain;
  const withdrawing = withdrawal.busy || withdrawal.uncertain;
  const operating = command.busy || command.uncertain || uploading || approving || withdrawing;
  const locked = dirty || Boolean(withdrawalReason) || operating;
  const lockNavigation = useEditGuard({
    dirty: dirty || Boolean(withdrawalReason),
    pending: command.busy || approval.busy || withdrawal.busy,
    uncertain: command.uncertain || approval.uncertain || withdrawal.uncertain,
    label: "Listing changes",
    discard: () => {
      const current = stale ? snapshot.draft : base;
      setBase(current);
      setValue(listingDraft(current));
      setDirty(false);
      setValidationAttempted(false);
      setWithdrawalReason("");
      setWithdrawalTarget(null);
      setWithdrawalValidationAttempted(false);
    },
  });
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

  const isWithdrawn = Boolean(withdrawnPublicationId);
  const hasActivePublication = Boolean(snapshot.publication && !isWithdrawn);
  const canApprove = approval.canApprove;
  const canWithdraw = Boolean(
    hasActivePublication &&
    !operating &&
    (!withdrawalTarget || withdrawalTarget === snapshot.publication?.id),
  );

  function changeWithdrawalReason(reason: string) {
    if (operating) {
      return;
    }
    setWithdrawalTarget(reason ? (withdrawalTarget ?? snapshot.publication?.id ?? null) : null);
    setWithdrawalReason(reason);
    if (!reason) {
      setWithdrawalValidationAttempted(false);
    }
    lockNavigation(dirty || Boolean(reason));
    onLock(dirty || Boolean(reason));
  }

  function selectPhoto(file: File | null) {
    if (operating || value.photoIds.length >= MAX_LISTING_PHOTOS || !upload.select(file)) {
      return false;
    }
    lockNavigation(true);
    onLock(true);
    return true;
  }

  function change(next: ListingDraft) {
    if (operating) {
      return;
    }
    if (next.photoIds.length > MAX_LISTING_PHOTOS && next.photoIds.length > value.photoIds.length) {
      toast.error("A listing can contain up to four photos. Remove one before adding another.");
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
    lockNavigation(Boolean(withdrawalReason));
    onLock(Boolean(withdrawalReason));
  }

  async function save() {
    if (uploading || approving || withdrawing || command.busy || (stale && !command.uncertain)) {
      return;
    }
    const contents = listingDraftContents(value);
    if (!contents && !command.uncertain) {
      setValidationAttempted(true);
      if (value.photoIds.length > MAX_LISTING_PHOTOS) {
        toast.error("Keep up to four photos in this listing before saving.");
        return;
      }
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
      lockNavigation(Boolean(withdrawalReason));
      onLock(Boolean(withdrawalReason));
      onSaved();
    }
  }

  async function approve() {
    if (approval.busy || (!approval.uncertain && !canApprove)) {
      return;
    }
    lockNavigation(true);
    onLock(true);
    const resolved = await approval.approve();
    if (resolved) {
      lockNavigation(false);
      onLock(false);
    }
  }

  async function withdraw() {
    if (withdrawal.busy || (!withdrawal.uncertain && !canWithdraw)) {
      return;
    }
    const reason = listingWithdrawalReason(withdrawalReason);
    if (!withdrawal.uncertain && !reason) {
      setWithdrawalValidationAttempted(true);
      toast.error(
        "Enter a withdrawal reason of at most 1000 characters without control characters.",
      );
      return;
    }
    lockNavigation(true);
    onLock(true);
    const result = await withdrawal.submit({
      operationId: crypto.randomUUID(),
      publicationId: withdrawalTarget ?? snapshot.publication?.id,
      reason,
    });
    if (result) {
      lockNavigation(dirty || Boolean(withdrawalReason));
      onLock(dirty || Boolean(withdrawalReason));
    }
    if (result?.status === 200 && result.data) {
      setWithdrawalReason("");
      setWithdrawalTarget(null);
      setWithdrawalValidationAttempted(false);
      lockNavigation(dirty);
      onLock(dirty);
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
    approval,
    canApprove,
    isWithdrawn,
    hasActivePublication,
    withdrawal,
    withdrawalReason,
    withdrawalValidationAttempted,
    withdrawalTargetChanged: Boolean(
      withdrawalTarget && withdrawalTarget !== snapshot.publication?.id,
    ),
    canWithdraw,
    changeWithdrawalReason,
    upload: { ...upload, select: selectPhoto },
    validationAttempted,
    change,
    discard,
    save,
    approve,
    withdraw,
  };
}
