"use client";
import Image from "next/image";
import { Button } from "@/components/ui/Button";
import { useSellerPhoto } from "@/hooks/use-seller-photos";
import type { PhotoAngle, PhotoReceipt } from "@/lib/intake/contracts";
import styles from "./Sell.module.css";

export function PhotoUpload({
  angle,
  disabled,
  onReceipt,
  onLock,
}: {
  angle: PhotoAngle;
  disabled: boolean;
  onReceipt: (angle: PhotoAngle, receipt: PhotoReceipt | null) => void;
  onLock: (angle: PhotoAngle, locked: boolean) => void;
}) {
  const photo = useSellerPhoto(angle, onReceipt, onLock);
  return (
    <section className={styles.upload} aria-label={`${angle} photo upload`}>
      <h3>{angle}</h3>
      {photo.preview && (
        <Image
          src={photo.preview}
          alt={`${angle} exterior preview`}
          width={400}
          height={250}
          unoptimized
          className={styles.preview}
        />
      )}
      <label className={styles.fileLabel}>
        <span>{angle} photo</span>
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp"
          disabled={disabled || photo.busy || photo.uncertain}
          onChange={(event) => photo.select(event.target.files?.[0] ?? null)}
        />
      </label>
      <p className={styles.help}>
        Show the full {angle.toLowerCase()} exterior. JPEG, PNG or WebP, up to 10 MiB.
      </p>
      <Button
        type="button"
        variant="secondary"
        disabled={disabled || !photo.file || photo.busy || Boolean(photo.receipt)}
        onClick={() => void photo.upload()}
      >
        {photo.uncertain ? "Retry same upload" : "Upload photo"}
      </Button>
    </section>
  );
}
