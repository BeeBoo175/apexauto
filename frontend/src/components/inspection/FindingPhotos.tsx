"use client";
import { useInspectionPhoto } from "@/hooks/use-inspection-photo";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";
import type { PhotoReceipt } from "@/lib/inspection/report-contracts";
import styles from "./Inspection.module.css";

export function FindingPhotos({
  inspectionId,
  pointCode,
  assignmentVersion,
  actorId,
  ids,
  disabled,
  onChange,
  onLock,
}: {
  inspectionId: string;
  pointCode: string;
  assignmentVersion: number;
  actorId: string;
  ids: string[];
  disabled: boolean;
  onChange: (ids: string[]) => void;
  onLock: (locked: boolean) => void;
}) {
  function attach(receipt: PhotoReceipt) {
    onChange([...ids, receipt.id]);
  }
  const photo = useInspectionPhoto(
    inspectionId,
    assignmentVersion,
    pointCode,
    actorId,
    attach,
    onLock,
  );
  return (
    <div className={styles.photoSection}>
      <p>Private evidence · {ids.length} of 4 photos</p>
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
      {ids.length < 4 && (
        <>
          <Input
            id={`photo-${pointCode}`}
            type="file"
            label="Add JPEG, PNG or WebP (up to 10 MiB)"
            accept="image/jpeg,image/png,image/webp"
            disabled={(disabled && !photo.file) || photo.busy || photo.uncertain}
            onChange={(event) => photo.select(event.target.files?.[0] ?? null)}
          />
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
