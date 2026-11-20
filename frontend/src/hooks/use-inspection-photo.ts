"use client";
import { useEffect, useRef, useState } from "react";
import { inspectionFetch } from "@/lib/inspection/browser";
import { parsePhoto } from "@/lib/inspection/parse-report";
import { PHOTO_UPLOAD_MAX_BYTES } from "@/lib/intake/contracts";
import type { PhotoReceipt } from "@/lib/inspection/report-contracts";
import { useToast } from "@/components/ui/Toast";
import { useEditGuard } from "./use-edit-guard";

export function useInspectionPhoto(
  inspectionId: string,
  assignmentVersion: number,
  pointCode: string,
  actorId: string,
  onReceipt: (receipt: PhotoReceipt) => void,
  onLock: (locked: boolean) => void,
) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const operation = useRef<{ id: string; file: File } | null>(null);
  const controller = useRef<AbortController | null>(null);
  const toast = useToast();
  useEditGuard({
    dirty: Boolean(file),
    pending: busy,
    uncertain,
    label: "Inspection photo",
    discard,
  });
  useEffect(() => () => controller.current?.abort(), []);

  function select(file: File | null) {
    if (!file || busy || uncertain) {
      return;
    }
    if (
      file.size > PHOTO_UPLOAD_MAX_BYTES ||
      !["image/jpeg", "image/png", "image/webp"].includes(file.type)
    ) {
      toast.error("Choose a JPEG, PNG or WebP photo no larger than 4 MiB.");
      return;
    }
    operation.current = { id: crypto.randomUUID(), file };
    setFile(file);
    onLock(true);
  }

  function discard() {
    if (busy || uncertain) {
      return;
    }
    operation.current = null;
    setFile(null);
    onLock(false);
  }

  async function upload(reconcile = false) {
    if (!operation.current || controller.current) {
      return;
    }
    const captured = operation.current;
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    onLock(true);
    const path = `drafts/${inspectionId}/photos/${captured.id}`;
    try {
      const options = {
        method: "PUT",
        headers: { "Content-Type": captured.file.type },
        body: captured.file,
        signal: attempt.signal,
      };
      const query = new URLSearchParams({
        pointCode,
        assignmentVersion: String(assignmentVersion),
      });
      const result = await inspectionFetch(
        reconcile ? `${path}/receipt` : `${path}?${query}`,
        parsePhoto,
        reconcile ? { signal: attempt.signal } : options,
        reconcile ? 22_000 : 92_000,
      );
      if (attempt.signal.aborted) {
        return;
      }
      if (result.uncertain || result.status >= 500 || result.status === 202) {
        throw new Error("Unconfirmed upload");
      }
      const receipt = result.data;
      if (
        receipt &&
        (receipt.id !== captured.id ||
          receipt.inspectionId !== inspectionId ||
          receipt.pointCode !== pointCode ||
          receipt.assignmentVersion !== assignmentVersion ||
          receipt.actorId !== actorId)
      ) {
        throw new Error("Photo receipt mismatch");
      }
      if (
        result.status === 200 &&
        receipt?.availableAt &&
        !receipt.removalRequestedAt &&
        !receipt.deletedAt
      ) {
        setUncertain(false);
        onReceipt(receipt);
        operation.current = null;
        setFile(null);
        onLock(false);
        toast.success("Photo confirmed. Save the draft to attach it.");
        return;
      }
      if (reconcile && result.status === 404) {
        toast.warning("No confirmed upload was found. Retry the same file and upload ID.");
        return;
      }
      setUncertain(false);
      onLock(false);
      toast.error(result.message || "Photo is unavailable. Choose a replacement.");
    } catch {
      if (!attempt.signal.aborted) {
        setUncertain(true);
        toast.warning(
          "Photo upload is unconfirmed. Reconcile or retry the same file and upload ID.",
        );
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }
  return { file, busy, uncertain, select, upload, discard };
}
