"use client";

import { useId, useRef, type ReactNode } from "react";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { useListingUpload } from "@/hooks/use-listing-upload";
import styles from "./ListingGallery.module.css";

export function ListingPhotoUpload({
  upload,
  disabled,
  children,
}: {
  upload: ReturnType<typeof useListingUpload>;
  disabled: boolean;
  children?: ReactNode;
}) {
  const fileInput = useRef<HTMLInputElement>(null);
  const helpId = useId();
  return (
    <div className={styles.gallery}>
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
      {!upload.file && (
        <div className={styles.photoActions}>
          <Button
            size="compact"
            variant="secondary"
            disabled={disabled}
            aria-describedby={helpId}
            title="JPEG, PNG or WebP · up to 10 MiB"
            onClick={() => fileInput.current?.click()}
          >
            Upload
          </Button>
          {children}
        </div>
      )}
      {upload.file && (
        <div className={styles.pendingUpload}>
          <span className={styles.fileName} title={upload.file.name}>
            {upload.file.name}
          </span>
          <div className={styles.photoActions}>
            <Button
              size="compact"
              variant="secondary"
              disabled={upload.busy}
              onClick={() => void upload.upload()}
            >
              {upload.uncertain ? "Retry upload" : "Upload photo"}
            </Button>
            {upload.uncertain ? (
              <Button
                size="compact"
                variant="secondary"
                disabled={upload.busy}
                onClick={() => void upload.upload(true)}
              >
                Check receipt
              </Button>
            ) : (
              <Button
                size="compact"
                variant="ghost"
                disabled={upload.busy}
                onClick={upload.discard}
              >
                Discard
              </Button>
            )}
          </div>
        </div>
      )}
      <span id={helpId} className="sr-only">
        JPEG, PNG or WebP, up to 10 MiB.
      </span>
    </div>
  );
}
