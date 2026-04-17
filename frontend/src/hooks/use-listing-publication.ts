"use client";

import { useEffect, useRef, useState } from "react";
import { useApiCommand } from "./use-api-command";
import { privateApiFetch } from "@/lib/api-browser";
import type { CurrentActor } from "@/lib/auth/actor";
import {
  listingDraft,
  listingDraftContents,
  matchesListingSave,
  parseListingReceipt,
  type ListingSaveReceipt,
} from "@/lib/vehicles/listing-draft";
import { MAX_LISTING_PHOTOS } from "@/lib/vehicles/listing-photos";
import {
  matchesListingPublication,
  parseListingPublicationReceipt,
} from "@/lib/vehicles/listing-publication";
import type { ListingRevision, ListingWorkspace } from "@/lib/vehicles/listing-workspace";

interface PublicationOperation {
  saveId: string;
  publicationId: string;
  signedRevisionId: string;
  source: ListingRevision;
  revision: ListingRevision | null;
  republish: boolean;
}

export function useListingPublication(
  snapshot: ListingWorkspace,
  actor: CurrentActor,
  {
    base,
    ready,
    withdrawnPublicationId,
    onPrepared,
    onSaved,
  }: {
    base: ListingRevision | null;
    ready: boolean;
    withdrawnPublicationId: string | null;
    onPrepared: (receipt: ListingSaveReceipt) => void;
    onSaved: () => void;
  },
) {
  const { vehicleId, branchId } = snapshot.vehicle;
  const [pending, setPending] = useState<PublicationOperation | null>(null);
  const [busy, setBusy] = useState(false);
  const running = useRef(false);
  const lifetime = useRef({ active: true });
  const preparation = useApiCommand(
    `/api/staff/listings/${vehicleId}/drafts`,
    parseListingReceipt,
    (receipt, body) =>
      receipt.vehicleId === vehicleId &&
      receipt.branchId === branchId &&
      receipt.actorId === actor.userId &&
      matchesListingSave(receipt, body),
    privateApiFetch,
  );
  const approval = useApiCommand(
    `/api/staff/listings/${vehicleId}/publications`,
    parseListingPublicationReceipt,
    (receipt, body) =>
      receipt.vehicleId === vehicleId &&
      receipt.branchId === branchId &&
      receipt.actorId === actor.userId &&
      matchesListingPublication(receipt, body),
    privateApiFetch,
  );
  useEffect(() => {
    const owner = { active: true };
    lifetime.current = owner;
    return () => {
      owner.active = false;
    };
  }, [actor.userId, actor.role, actor.branchId, vehicleId, branchId]);

  const uncertain = preparation.uncertain || approval.uncertain;
  const needsRevision = Boolean(
    base &&
    (snapshot.publication?.revisionId === base.id || approval.receipt?.revisionId === base.id),
  );
  const canApprove = Boolean(
    ready &&
    base &&
    snapshot.currentSignedRevisionId &&
    base.photoIds.length <= MAX_LISTING_PHOTOS &&
    !busy &&
    !uncertain &&
    (!needsRevision || withdrawnPublicationId) &&
    (!approval.receipt ||
      approval.receipt.revisionId !== base.id ||
      approval.receipt.id === withdrawnPublicationId),
  );

  async function prepare(operation: PublicationOperation, owner: { active: boolean }) {
    if (operation.revision) {
      return { operation, resolved: true };
    }
    const contents = listingDraftContents(listingDraft(operation.source));
    if (!contents) {
      setPending(null);
      return { operation: null, resolved: true };
    }
    const saved = await preparation.submit({
      operationId: operation.saveId,
      expectedVersion: operation.source.version,
      ...contents,
    });
    if (!owner.active || !saved) {
      return { operation: null, resolved: false };
    }
    if (saved.status !== 200 || !saved.data) {
      setPending(null);
      return { operation: null, resolved: true };
    }
    const next = { ...operation, revision: saved.data };
    setPending(next);
    onPrepared(saved.data);
    return { operation: next, resolved: true };
  }

  async function approve() {
    if (running.current || (!pending && !canApprove)) {
      return false;
    }
    const owner = lifetime.current;
    let operation = pending;
    if (!operation) {
      if (!base || !snapshot.currentSignedRevisionId) {
        return false;
      }
      operation = {
        saveId: crypto.randomUUID(),
        publicationId: crypto.randomUUID(),
        signedRevisionId: snapshot.currentSignedRevisionId,
        source: { ...base, photoIds: [...base.photoIds] },
        revision: needsRevision ? null : base,
        republish: Boolean(withdrawnPublicationId),
      };
      setPending(operation);
    }
    running.current = true;
    setBusy(true);
    try {
      const prepared = await prepare(operation, owner);
      if (!prepared.operation) {
        return prepared.resolved;
      }
      operation = prepared.operation;
      if (!operation.revision || !owner.active) {
        return false;
      }
      // Approval remains bound to the saved revision and signed inspection captured by the original click.
      const result = await approval.submit({
        operationId: operation.publicationId,
        revisionId: operation.revision.id,
        signedRevisionId: operation.signedRevisionId,
      });
      if (!owner.active || !result) {
        return false;
      }
      setPending(null);
      if (result.status === 200 || operation.revision.id !== operation.source.id) {
        onSaved();
      }
      return true;
    } finally {
      running.current = false;
      if (owner.active) {
        setBusy(false);
      }
    }
  }

  return {
    busy,
    uncertain,
    receipt: approval.receipt,
    awaitingRefresh: Boolean(
      approval.receipt &&
      approval.receipt.revisionId === base?.id &&
      approval.receipt.id !== withdrawnPublicationId &&
      approval.receipt.id !== snapshot.publication?.id,
    ),
    canApprove,
    republishing: Boolean(pending?.republish),
    approve,
  };
}
