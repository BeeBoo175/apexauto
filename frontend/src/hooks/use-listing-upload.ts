"use client";

import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import { apiFetch } from "@/lib/api-browser";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import { matchesListingUpload, parseListingPhotoReceipt } from "@/lib/vehicles/listing-photos";

export function useListingUpload(vehicleId: string, onConfirmed: (id: string) => boolean) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const operation = useRef<{ id: string; file: File } | null>(null);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useEffect(() => () => controller.current?.abort(), []);

  function select(selected: File | null) {
    if (!selected || operation.current || controller.current) {
      return false;
    }
    if (
      selected.size === 0 ||
      selected.size > PHOTO_MAX_BYTES ||
      !["image/jpeg", "image/png", "image/webp"].includes(selected.type)
    ) {
      toast.error("Choose a JPEG, PNG or WebP photo no larger than 10 MiB.");
      return false;
    }
    operation.current = { id: crypto.randomUUID(), file: selected };
    setFile(selected);
    return true;
  }

  function discard() {
    if (controller.current || uncertain) {
      return;
    }
    operation.current = null;
    setFile(null);
  }

  async function upload(reconcile = false) {
    if (!operation.current || controller.current) {
      return;
    }
    const captured = operation.current;
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    const path = `/api/staff/listings/${vehicleId}/photos/${captured.id}`;
    try {
      const result = await apiFetch(
        reconcile ? `${path}/receipt` : path,
        parseListingPhotoReceipt,
        reconcile
          ? { signal: attempt.signal }
          : {
              method: "PUT",
              headers: { "Content-Type": captured.file.type },
              body: captured.file,
              signal: attempt.signal,
            },
        { deadlineMs: reconcile ? 22_000 : 92_000, requireActor: true },
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.uncertain || result.status >= 500 || result.status === 202) {
        throw new Error("Unconfirmed upload");
      }
      const receipt = result.data;
      if (receipt && !matchesListingUpload(receipt, vehicleId, captured)) {
        throw new Error("Photo receipt mismatch");
      }
      if (result.status === 200 && receipt?.availableAt) {
        const added = onConfirmed(receipt.id);
        operation.current = null;
        setFile(null);
        setUncertain(false);
        toast.success(
          added
            ? "Photo confirmed. Save the draft to keep it in this listing."
            : "Photo confirmed in uploaded photos. Remove a listing photo before adding it.",
        );
        return;
      }
      if (reconcile && result.status === 404) {
        toast.warning("No confirmed upload was found. Retry the same file and upload ID.");
        return;
      }
      if (result.status === 200) {
        throw new Error("Photo availability unconfirmed");
      }
      setUncertain(false);
      toast.error(result.message || "Photo is unavailable. Choose a replacement.");
    } catch {
      if (!attempt.signal.aborted) {
        setUncertain(true);
        toast.warning("Photo upload is unconfirmed. Check its receipt or retry the same photo.");
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  return { file, busy, uncertain, select, discard, upload };
}
