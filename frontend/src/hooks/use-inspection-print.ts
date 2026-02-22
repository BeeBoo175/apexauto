"use client";
import { useEffect, useRef, useState } from "react";

async function decodePhoto(photo: HTMLImageElement, signal: AbortSignal) {
  signal.throwIfAborted();
  let rejectAborted: (() => void) | null = null;
  const aborted = new Promise<never>((_, reject) => {
    rejectAborted = () => reject(new Error("Print preparation ended"));
    signal.addEventListener("abort", rejectAborted, { once: true });
  });
  try {
    await Promise.race([photo.decode(), aborted]);
    signal.throwIfAborted();
  } finally {
    if (rejectAborted) {
      signal.removeEventListener("abort", rejectAborted);
    }
  }
}

export function useInspectionPrint() {
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState("");
  const [message, setMessage] = useState("");
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  async function print() {
    if (controller.current) {
      return;
    }
    const attempt = new AbortController();
    controller.current = attempt;
    setBusy(true);
    setMessage("");
    const photos = Array.from(
      document.querySelectorAll<HTMLImageElement>("[data-inspection-report] img"),
    );
    const signal = AbortSignal.any([attempt.signal, AbortSignal.timeout(90_000)]);
    let loaded = photos.filter((photo) => photo.complete && photo.naturalWidth > 0).length;
    let requested = 0;
    try {
      for (const photo of photos) {
        signal.throwIfAborted();
        if (photo.complete && photo.naturalWidth > 0) {
          continue;
        }
        if (requested === 40) {
          setMessage(
            `${loaded} of ${photos.length} evidence photos loaded. Continue preparation to load the next batch.`,
          );
          return;
        }
        requested++;
        setProgress(`${loaded} / ${photos.length}`);
        photo.loading = "eager";
        if (photo.complete && photo.naturalWidth === 0) {
          const source = new URL(photo.src);
          source.searchParams.set("retry", crypto.randomUUID());
          photo.src = source.toString();
        }
        await decodePhoto(photo, AbortSignal.any([signal, AbortSignal.timeout(42_000)]));
        loaded++;
      }
      signal.throwIfAborted();
      window.print();
    } catch {
      if (!attempt.signal.aborted) {
        setMessage(
          `${loaded} of ${photos.length} evidence photos loaded. Some evidence is unavailable. Loaded photos remain; retry preparation when the service or request budget is available.`,
        );
      }
    } finally {
      setBusy(false);
      controller.current = null;
    }
  }
  return { busy, progress, message, print };
}
