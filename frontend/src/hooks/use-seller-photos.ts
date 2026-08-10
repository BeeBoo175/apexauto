"use client";
import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import { parsePhoto } from "@/lib/intake/parse";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import type { PhotoAngle, PhotoReceipt } from "@/lib/intake/contracts";
import { useToast } from "@/components/ui/Toast";

export interface SellerPhotoController {
  angle: PhotoAngle;
  file: File | null;
  preview: string;
  busy: boolean;
  uncertain: boolean;
  receipt: PhotoReceipt | null;
  select: (selected: File | null) => void;
  upload: () => Promise<boolean>;
}

export function useSellerPhoto(
  angle: PhotoAngle,
  onReceipt: (angle: PhotoAngle, receipt: PhotoReceipt | null) => void,
  onLock: (angle: PhotoAngle, locked: boolean) => void,
): SellerPhotoController {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [receipt, setReceipt] = useState<PhotoReceipt | null>(null);
  const operation = useRef<{ id: string; file: File } | null>(null);
  const controller = useRef<AbortController | null>(null);
  const receiptRef = useRef<PhotoReceipt | null>(null);
  const { success, warning, error } = useToast();

  useEffect(() => {
    if (!file) {
      return;
    }
    const url = URL.createObjectURL(file);
    queueMicrotask(() => setPreview(url));
    return () => URL.revokeObjectURL(url);
  }, [file]);
  useEffect(() => () => controller.current?.abort(), []);
  useEffect(() => {
    if (!busy && !uncertain) {
      return;
    }
    const guard = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", guard);
    return () => window.removeEventListener("beforeunload", guard);
  }, [busy, uncertain]);

  function select(selected: File | null) {
    if (busy || uncertain || !selected) {
      return;
    }
    if (
      selected.size > PHOTO_MAX_BYTES ||
      !["image/jpeg", "image/png", "image/webp"].includes(selected.type)
    ) {
      error("Choose a JPEG, PNG or WebP photo no larger than 10 MiB.");
      return;
    }
    setFile(selected);
    operation.current = { id: crypto.randomUUID(), file: selected };
    receiptRef.current = null;
    setReceipt(null);
    onReceipt(angle, null);
  }

  async function upload(): Promise<boolean> {
    if (receiptRef.current) {
      return true;
    }
    if (!operation.current || controller.current) {
      return false;
    }
    const captured = operation.current;
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    onLock(angle, true);
    try {
      const result = await apiFetch(
        `/api/intake/photos/${captured.id}?angle=${angle}`,
        parsePhoto,
        {
          method: "PUT",
          headers: { "Content-Type": captured.file.type },
          body: captured.file,
          signal: attempt.signal,
        },
        { deadlineMs: PHOTO_DEADLINE_MS + 2000 },
      );
      if (attempt.signal.aborted) {
        return false;
      }
      if (result.uncertain || result.status >= 500 || result.status === 202) {
        throw new Error("Unconfirmed upload");
      }
      setUncertain(false);
      if (
        result.status === 200 &&
        (result.data?.id !== captured.id || result.data.angle !== angle)
      ) {
        throw new Error("Unconfirmed photo identity");
      }
      onLock(angle, false);
      if (
        result.data?.id !== captured.id ||
        result.data.angle !== angle ||
        result.data.status !== "Available"
      ) {
        error(result.message || "The photo is unavailable. Choose a replacement.");
        receiptRef.current = null;
        onReceipt(angle, null);
        return false;
      }
      receiptRef.current = result.data;
      setReceipt(result.data);
      onReceipt(angle, result.data);
      success(`${angle} photo confirmed.`);
      return true;
    } catch {
      if (!attempt.signal.aborted) {
        setUncertain(true);
        warning(`${angle} upload is unconfirmed. Retry the same upload.`);
      }
      return false;
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  return { angle, file, preview, busy, uncertain, receipt, select, upload };
}

export function useSellerPhotos(
  onReceipt: (angle: PhotoAngle, receipt: PhotoReceipt | null) => void,
  onLock: (angle: PhotoAngle, locked: boolean) => void,
) {
  const driver = useSellerPhoto("Driver", onReceipt, onLock);
  const passenger = useSellerPhoto("Passenger", onReceipt, onLock);
  const front = useSellerPhoto("Front", onReceipt, onLock);
  const rear = useSellerPhoto("Rear", onReceipt, onLock);
  const items = [driver, passenger, front, rear] as const;

  const readyCount = items.filter((item) => Boolean(item.file || item.receipt)).length;
  const uploadedCount = items.filter((item) => Boolean(item.receipt)).length;
  const uploading = items.some((item) => item.busy);
  const anyUncertain = items.some((item) => item.uncertain);

  async function uploadPending(): Promise<boolean> {
    let allConfirmed = true;
    for (const item of items) {
      if (item.receipt) {
        continue;
      }
      if (!item.file) {
        allConfirmed = false;
        continue;
      }
      const ok = await item.upload();
      if (!ok) {
        allConfirmed = false;
      }
    }
    return allConfirmed;
  }

  return { items, readyCount, uploadedCount, uploading, anyUncertain, uploadPending };
}
