"use client";

import { useState } from "react";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import type { ListingDraft } from "@/lib/vehicles/listing-draft";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { VehicleCardView } from "./VehicleCardView";
import catalogue from "./Catalogue.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";

export function ListingCardPreview({
  snapshot,
  draft,
}: {
  snapshot: ListingWorkspace;
  draft: ListingDraft;
}) {
  const [entry, setEntry] = useState(snapshot);
  const [photoVersion, setPhotoVersion] = useState(0);
  if (entry !== snapshot) {
    setEntry(snapshot);
    setPhotoVersion((current) => current + 1);
  }
  const vehicle = snapshot.vehicle;
  const price = parseMyr(draft.vehiclePrice);
  const coverPhotoId = draft.photoIds[0];

  return (
    <section className={workbench.stack} aria-label="Catalogue card preview">
      <h3>Catalogue card preview</h3>
      <p>
        This preview uses your current draft. Save the draft before a separate publication approval.
      </p>
      {price === null && <p>Enter a valid vehicle price before saving.</p>}
      <div className={catalogue.vehicles}>
        <VehicleCardView
          key={`${vehicle.vehicleId}:${coverPhotoId ?? "none"}`}
          vehicle={{
            ...vehicle,
            transmission: draft.transmission || "Not set",
            fuel: draft.fuel || "Not set",
          }}
          price={price === null ? draft.vehiclePrice || "Not set" : formatCurrency(price)}
          photoUrl={
            coverPhotoId ? `/api/staff/listings/${vehicle.vehicleId}/photos/${coverPhotoId}` : null
          }
          first
          photoVersion={photoVersion}
        />
      </div>
    </section>
  );
}
