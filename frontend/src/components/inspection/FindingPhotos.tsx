"use client";
import { useId, useRef } from "react";
import type { useInspectionPhoto } from "@/hooks/use-inspection-photo";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import styles from "./Inspection.module.css";

export function FindingPhotos({
  inspectionId,
  pointCode,
  photo,
  ids,
  disabled,
  onChange,
}: {
  inspectionId: string;
  pointCode: string;
  photo: ReturnType<typeof useInspectionPhoto>;
  ids: string[];
  disabled: boolean;
  onChange: (ids: string[]) => void;
}) {
  const fileInput = useRef<HTMLInputElement>(null);
  const formatHelpId = useId();
  const canAdd = ids.length < 4;
  return (
    <div className={styles.photoSection}>
      {(ids.length > 0 || (canAdd && !photo.file)) && (
        <div className={styles.photoHeader}>
          {ids.length > 0 && <p>Private evidence · {ids.length} of 4 photos</p>}
          {canAdd && !photo.file && (
            <>
              <Button
                type="button"
                size="compact"
                variant="secondary"
                disabled={disabled || photo.busy || photo.uncertain}
                aria-describedby={formatHelpId}
                onClick={() => fileInput.current?.click()}
              >
                Add evidence
              </Button>
              <span id={formatHelpId} className={styles.help}>
                JPEG, PNG or WebP · up to 4 MiB
              </span>
            </>
          )}
        </div>
      )}
      {ids.length > 0 && (
        <div className={styles.photos}>
          {ids.map((id) => (
            <figure key={id}>
              {/* Private authenticated evidence must use the same-origin route without an image cache. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={`/api/staff/inspection/drafts/${inspectionId}/photos/${id}`}
                alt={`${pointCode} inspection evidence`}
                width={160}
                height={110}
              />
              <Button
                type="button"
                size="compact"
                variant="secondary"
                disabled={disabled || photo.busy || photo.uncertain}
                onClick={() => onChange(ids.filter((value) => value !== id))}
              >
                Detach photo
              </Button>
            </figure>
          ))}
        </div>
      )}
      {(canAdd || photo.file || photo.uncertain) && (
        <>
          <div hidden>
            <Input
              ref={fileInput}
              id={`photo-${pointCode}`}
              type="file"
              label="Add JPEG, PNG or WebP (up to 4 MiB)"
              accept="image/jpeg,image/png,image/webp"
              disabled={disabled || photo.busy || photo.uncertain}
              onChange={(event) => {
                photo.select(event.target.files?.[0] ?? null);
                event.target.value = "";
              }}
            />
          </div>
          {photo.file && (
            <div className={styles.actions}>
              <span>{photo.file.name}</span>
              <Button
                type="button"
                size="compact"
                variant="secondary"
                disabled={photo.busy}
                onClick={() => void photo.upload()}
              >
                {photo.uncertain ? "Retry same photo" : "Upload photo"}
              </Button>
              {!photo.uncertain && (
                <Button
                  type="button"
                  size="compact"
                  variant="secondary"
                  disabled={photo.busy}
                  onClick={photo.discard}
                >
                  Discard selected photo
                </Button>
              )}
              {photo.uncertain && (
                <Button
                  type="button"
                  size="compact"
                  variant="secondary"
                  disabled={photo.busy}
                  onClick={() => void photo.upload(true)}
                >
                  Check photo receipt
                </Button>
              )}
            </div>
          )}
          {photo.uncertain && (
            <p role="alert">
              Upload is unconfirmed. The file and upload ID are retained. Reconcile before changing
              this finding or leaving.
            </p>
          )}
        </>
      )}
    </div>
  );
}
