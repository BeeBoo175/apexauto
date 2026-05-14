"use client";

import { useState } from "react";
import { Dialog } from "@/components/ui/Dialog";
import { Button } from "@/components/ui/Button";
import type { ReportPoint, ReportPhoto } from "@/lib/inspection/report-contracts";
import { formatCurrency } from "@/lib/currency-format";
import evidence from "./SignedFinding.module.css";
import record from "@/components/staff/StaffRecordContent.module.css";

export function SignedFinding({
  point,
  inspectionId,
  revision,
}: {
  point: ReportPoint;
  inspectionId: string;
  revision: number;
}) {
  const [selected, setSelected] = useState<ReportPhoto | null>(null);
  const hasPhotos = point.photos.length > 0;
  const hasFinding = point.kind === "Finding";
  if (!hasFinding && !point.notes && !hasPhotos) {
    return null;
  }
  return (
    <>
      <div className={record.group}>
        <h4>{`Original signed finding · revision ${revision}`}</h4>
        <p>{point.notes || "No finding recorded for this inspection point."}</p>
      </div>
      {(hasFinding || hasPhotos) && (
        <div className={evidence.facts}>
          {hasFinding && (
            <span>
              Original estimate <strong>{formatCurrency(point.estimatedRepairCost)}</strong>
            </span>
          )}
          {hasPhotos && <span>{point.photos.length} evidence images</span>}
        </div>
      )}
      {hasPhotos && (
        <div className={evidence.photos}>
          {point.photos.map((photo, index) => (
            <figure key={photo.id}>
              {/* Signed evidence stays on the authenticated same-origin media endpoint. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={`/api/staff/inspection/drafts/${inspectionId}/photos/${photo.id}`}
                alt={`${point.code} signed evidence ${index + 1}`}
                loading="lazy"
                width={photo.width}
                height={photo.height}
              />
              <Button
                type="button"
                size="compact"
                variant="ghost"
                onClick={() => setSelected(photo)}
                aria-label={`Open ${point.code} evidence ${index + 1}`}
              >
                View evidence {index + 1}
              </Button>
            </figure>
          ))}
        </div>
      )}
      <Dialog
        open={Boolean(selected)}
        title={`Signed evidence · ${point.code}`}
        onClose={() => setSelected(null)}
      >
        {selected && (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            className={evidence.image}
            src={`/api/staff/inspection/drafts/${inspectionId}/photos/${selected.id}`}
            alt={`${point.code} original signed evidence`}
            width={selected.width}
            height={selected.height}
          />
        )}
      </Dialog>
    </>
  );
}
