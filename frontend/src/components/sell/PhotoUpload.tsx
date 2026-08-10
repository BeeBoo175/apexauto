"use client";

import Image from "next/image";
import { useId } from "react";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import type { SellerPhotoController } from "@/hooks/use-seller-photos";
import type { PhotoAngle } from "@/lib/intake/contracts";
import styles from "./Sell.module.css";

const angleHints: Record<PhotoAngle, string> = {
  Driver: "Full left-side profile",
  Passenger: "Full right-side profile",
  Front: "Full front bumper and lights",
  Rear: "Full rear view and plate",
};

export function PhotoUpload({
  photo,
  disabled,
}: {
  photo: SellerPhotoController;
  disabled: boolean;
}) {
  const inputId = useId();
  const pickerDisabled = disabled || photo.busy || photo.uncertain;

  let badgeLabel = "";
  let badgeTone: "selected" | "uploaded" | "uncertain" = "selected";
  if (photo.uncertain) {
    badgeLabel = "Retry needed";
    badgeTone = "uncertain";
  } else if (photo.busy) {
    badgeLabel = "Uploading";
    badgeTone = "selected";
  } else if (photo.receipt) {
    badgeLabel = "Uploaded";
    badgeTone = "uploaded";
  } else if (photo.file) {
    badgeLabel = "Ready";
    badgeTone = "selected";
  }

  return (
    <section
      className={styles.upload}
      data-has-photo={Boolean(photo.preview) || undefined}
      data-uploaded={Boolean(photo.receipt) || undefined}
      data-uncertain={photo.uncertain || undefined}
      aria-label={`${photo.angle} photo upload`}
    >
      <input
        id={inputId}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        disabled={pickerDisabled}
        aria-label={`${photo.angle} photo`}
        className="sr-only"
        onChange={(event) => {
          photo.select(event.target.files?.[0] ?? null);
          event.target.value = "";
        }}
      />

      {photo.preview ? (
        <div className={styles.previewFrame}>
          <Image
            src={photo.preview}
            alt={`${photo.angle} exterior preview`}
            fill
            unoptimized
            className={styles.previewImage}
          />
          <div className={styles.previewTopBar}>
            <span className={styles.angleBadge}>{photo.angle}</span>
            {badgeLabel && (
              <span className={styles.statusPill} data-tone={badgeTone}>
                {photo.receipt && <Icon name="check" />}
                <span>{badgeLabel}</span>
              </span>
            )}
          </div>
          <div className={styles.previewBottomBar}>
            <span className={styles.fileName} title={photo.file?.name}>
              {photo.file?.name ?? `${photo.angle} photo`}
            </span>
            {photo.uncertain ? (
              <Button
                type="button"
                size="compact"
                variant="primary"
                disabled={photo.busy}
                onClick={() => void photo.upload()}
              >
                Retry same upload
              </Button>
            ) : (
              <label
                htmlFor={inputId}
                className={styles.replaceAction}
                aria-disabled={pickerDisabled || undefined}
              >
                Change
              </label>
            )}
          </div>
        </div>
      ) : (
        <label
          htmlFor={inputId}
          className={styles.dropZone}
          aria-disabled={pickerDisabled || undefined}
        >
          <span className={styles.dropIcon} aria-hidden="true">
            <Icon name="car" />
          </span>
          <strong className={styles.dropTitle}>{photo.angle}</strong>
          <span className={styles.dropHint}>{angleHints[photo.angle]}</span>
          <span className={styles.dropCta}>Choose photo</span>
        </label>
      )}
    </section>
  );
}
