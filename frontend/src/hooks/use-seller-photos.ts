"use client";
import { useEffect, useRef, useState } from "react";
import { apiFetch } from "@/lib/api-browser";
import { parsePhoto } from "@/lib/intake/parse";
import { PHOTO_DEADLINE_MS, PHOTO_MAX_BYTES } from "@/lib/intake/contracts";
import type { PhotoAngle, PhotoReceipt } from "@/lib/intake/contracts";
import { useToast } from "@/components/ui/Toast";

export function useSellerPhoto(
  angle: PhotoAngle,
  onReceipt: (angle: PhotoAngle, receipt: PhotoReceipt | null) => void,
  onLock: (angle: PhotoAngle, locked: boolean) => void,
) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState("");
  const [busy, setBusy] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [receipt, setReceipt] = useState<PhotoReceipt | null>(null);
  const operation = useRef<{ id: string; file: File } | null>(null);
  const controller = useRef<AbortController | null>(null);
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
    setReceipt(null);
    onReceipt(angle, null);
  }

  async function upload() {
    if (!operation.current || controller.current) {
      return;
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
        return;
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
        onReceipt(angle, null);
        return;
      }
      setReceipt(result.data);
      onReceipt(angle, result.data);
      success(`${angle} photo confirmed.`);
    } catch {
      if (!attempt.signal.aborted) {
        setUncertain(true);
        warning(`${angle} upload is unconfirmed. Retry the same upload.`);
      }
    } finally {
      if (!attempt.signal.aborted) {
        setBusy(false);
      }
      controller.current = null;
    }
  }

  return { file, preview, busy, uncertain, receipt, select, upload };
}
