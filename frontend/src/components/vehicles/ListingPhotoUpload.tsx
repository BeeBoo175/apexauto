"use client";

import { useId, useRef } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { useListingUpload } from "@/hooks/use-listing-upload";
import styles from "./ListingGallery.module.css";

export function ListingPhotoUpload({
  upload,
  disabled,
}: {
  upload: ReturnType<typeof useListingUpload>;
  disabled: boolean;
}) {
  const fileInput = useRef<HTMLInputElement>(null);
  const helpId = useId();
  return (
    <section className={styles.gallery} aria-label="Upload listing photo">
      <div hidden>
        <Input
          ref={fileInput}
          type="file"
          label="New listing photo"
          accept="image/jpeg,image/png,image/webp"
          disabled={disabled || Boolean(upload.file)}
          onChange={(event) => {
            upload.select(event.target.files?.[0] ?? null);
            event.target.value = "";
          }}
        />
      </div>
      <div className={styles.controls}>
        {!upload.file && (
          <Button
            size="compact"
            variant="secondary"
            disabled={disabled}
            aria-describedby={helpId}
            onClick={() => fileInput.current?.click()}
          >
            Add new photo
          </Button>
        )}
        {upload.file && (
          <>
            <span>{upload.file.name}</span>
            <Button
              size="compact"
              variant="secondary"
              disabled={upload.busy}
              onClick={() => void upload.upload()}
            >
              {upload.uncertain ? "Retry same photo" : "Upload photo"}
            </Button>
            {upload.uncertain ? (
              <Button
                size="compact"
                variant="secondary"
                disabled={upload.busy}
                onClick={() => void upload.upload(true)}
              >
                Check photo receipt
              </Button>
            ) : (
              <Button
                size="compact"
                variant="ghost"
                disabled={upload.busy}
                onClick={upload.discard}
              >
                Discard selected photo
              </Button>
            )}
          </>
        )}
      </div>
      <p id={helpId}>JPEG, PNG or WebP · up to 10 MiB. Confirmed photos join this draft.</p>
    </section>
  );
}
