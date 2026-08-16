"use client";
import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import {
  PICTURE_CONTENT_TYPES,
  parsePictureReceipt,
  parseRemovedPicture,
} from "@/lib/profile/contracts";
import { privateApiFetch } from "@/lib/api-browser";
import { PHOTO_MAX_BYTES } from "@/lib/intake/contracts";

// The owner replaces or removes the current picture; a replay of either leaves the same result.
export function useAccountPicture(accountId: string, initialPictureId: string | null) {
  const [pictureId, setPictureId] = useState(initialPictureId);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const { success, error, warning } = useToast();

  useEffect(() => () => controller.current?.abort(), []);

  async function run(command: () => Promise<boolean>, confirmed: string) {
    if (controller.current) {
      return;
    }
    const operation = new AbortController();
    controller.current = operation;
    setBusy(true);
    try {
      if (await command()) {
        success(confirmed);
      }
    } catch {
      if (!operation.signal.aborted) {
        warning("The result is unconfirmed. Check your picture and try again.");
      }
    } finally {
      controller.current = null;
      if (!operation.signal.aborted) {
        setBusy(false);
      }
    }
  }

  async function upload(file: File) {
    if (!PICTURE_CONTENT_TYPES.includes(file.type)) {
      error("Choose a JPEG, PNG or WebP picture.");
      return;
    }
    if (file.size > PHOTO_MAX_BYTES) {
      error("Choose a picture under 10 MiB.");
      return;
    }
    await run(async () => {
      const result = await privateApiFetch(
        "/api/accounts/me/picture",
        parsePictureReceipt,
        {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
          signal: controller.current?.signal,
        },
        { deadlineMs: 92_000 },
      );
      if (result.uncertain || result.status >= 500) {
        throw new Error("Unconfirmed picture upload");
      }
      if (result.status === 200 && result.data) {
        setPictureId(result.data.pictureId);
        return true;
      }
      error(result.message);
      return false;
    }, "Picture updated.");
  }

  async function remove() {
    await run(async () => {
      const result = await privateApiFetch(
        `/api/accounts/${accountId}/picture`,
        parseRemovedPicture,
        { method: "DELETE", signal: controller.current?.signal },
        { deadlineMs: 42_000 },
      );
      if (result.uncertain || result.status >= 500) {
        throw new Error("Unconfirmed picture removal");
      }
      if (result.status === 200 || result.status === 404) {
        setPictureId(null);
        return true;
      }
      error(result.message);
      return false;
    }, "Picture removed.");
  }

  return { pictureId, busy, upload, remove };
}
