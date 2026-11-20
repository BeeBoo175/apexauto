"use client";
import { useEffect, useRef, useState } from "react";
import { useToast } from "@/components/ui/Toast";
import {
  PICTURE_CONTENT_TYPES,
  parsePictureReceipt,
  parseRemovedPicture,
} from "@/lib/profile/contracts";
import { privateApiFetch } from "@/lib/api-browser";
import { PHOTO_UPLOAD_MAX_BYTES } from "@/lib/intake/contracts";

// The owner replaces or removes the picture they see. An upload keeps one id per chosen file so a replay
// reconciles instead of duplicating, and a removal names the displayed picture so a replacement is never removed.
export function useAccountPicture(accountId: string, initialPictureId: string | null) {
  const [pictureId, setPictureId] = useState(initialPictureId);
  const [busy, setBusy] = useState(false);
  const controller = useRef<AbortController | null>(null);
  const attempt = useRef<{ file: File; id: string } | null>(null);
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

  // After a lost response the stored receipt says whether the picture became current.
  async function reconcile(id: string) {
    const receipt = await privateApiFetch(
      `/api/accounts/me/picture/${id}/receipt`,
      parsePictureReceipt,
      { signal: controller.current?.signal },
      { deadlineMs: 22_000 },
    );
    return receipt.status === 200 && receipt.data?.status === "Available";
  }

  async function upload(file: File) {
    if (!PICTURE_CONTENT_TYPES.includes(file.type)) {
      error("Choose a JPEG, PNG or WebP picture.");
      return;
    }
    if (file.size > PHOTO_UPLOAD_MAX_BYTES) {
      error("Choose a picture no larger than 4 MiB.");
      return;
    }
    const chosen =
      attempt.current?.file === file ? attempt.current : { file, id: crypto.randomUUID() };
    attempt.current = chosen;
    await run(async () => {
      const result = await privateApiFetch(
        `/api/accounts/me/picture/${chosen.id}`,
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
        if (await reconcile(chosen.id)) {
          attempt.current = null;
          setPictureId(chosen.id);
          return true;
        }
        throw new Error("Unconfirmed picture upload");
      }
      if (result.status === 200 && result.data?.status === "Available") {
        attempt.current = null;
        setPictureId(result.data.pictureId);
        return true;
      }
      attempt.current = null;
      error(result.message || "The picture could not be saved. Choose it again.");
      return false;
    }, "Picture updated.");
  }

  async function remove() {
    if (!pictureId) {
      return;
    }
    const target = pictureId;
    await run(async () => {
      const result = await privateApiFetch(
        `/api/accounts/${accountId}/picture/${target}`,
        parseRemovedPicture,
        { method: "DELETE", signal: controller.current?.signal },
        { deadlineMs: 42_000 },
      );
      if (result.uncertain || result.status >= 500) {
        throw new Error("Unconfirmed picture removal");
      }
      if (result.status === 200 && result.data?.removalReason === "Removed") {
        setPictureId(null);
        return true;
      }
      // The picture on screen was already replaced or is not current: leave the account as it is.
      warning("Your picture changed elsewhere. Reload the page to see the current one.");
      return false;
    }, "Picture removed.");
  }

  return { pictureId, busy, upload, remove };
}
