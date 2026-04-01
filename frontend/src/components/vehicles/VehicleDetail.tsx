"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { useVehicleDetail } from "@/hooks/use-vehicle-detail";
import type { ApiResult } from "@/lib/api";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDate } from "@/lib/date-format";
import { label } from "@/lib/inspection/contracts";
import type { PublicVehicleDetail } from "@/lib/vehicles/public-detail";
import { ListingPhoto } from "./ListingGallery";
import { VehicleFinancing } from "./VehicleFinancing";
import gallery from "./ListingGallery.module.css";
import styles from "./VehicleDetail.module.css";

export function VehicleDetail({
  id,
  initial,
}: {
  id: string;
  initial: ApiResult<PublicVehicleDetail>;
}) {
  const { snapshot, unavailable, busy, refresh } = useVehicleDetail(id, initial);
  const listing = snapshot?.listing;
  const name = listing
    ? `${listing.manufactureYear} ${listing.make} ${listing.model} ${listing.trim}`
    : "Vehicle details";

  return (
    <section className={styles.page}>
      <Link href="/vehicles" prefetch={false}>
        Back to vehicles
      </Link>
      <header className={styles.heading}>
        <h1>{name}</h1>
        <Button variant="secondary" disabled={busy} onClick={() => void refresh()}>
          {busy ? "Refreshing vehicle…" : "Refresh vehicle"}
        </Button>
      </header>
      {!snapshot || !listing ? (
        <p>
          {unavailable
            ? "This vehicle is currently unavailable. Browse the catalogue for available vehicles."
            : "Refresh to check this vehicle's details and availability."}
        </p>
      ) : (
        <div aria-busy={busy} className={styles.content}>
          <p>
            {listing.branchName} · {listing.mileageKilometres.toLocaleString("en-MY")} km ·{" "}
            {listing.transmission} · {label(listing.fuel)}
          </p>
          <section aria-label="Vehicle photos" className={gallery.gallery}>
            {snapshot.photoIds.length === 0 && <p>Photography unavailable</p>}
            <div className={gallery.photos}>
              {snapshot.photoIds.map((photoId, index) => (
                <ListingPhoto
                  key={`${id}:${photoId}`}
                  vehicleId={id}
                  id={photoId}
                  position={index + 1}
                  vehicleName={name}
                  photoUrl={`/api/vehicles/${id}/photos/${photoId}`}
                />
              ))}
            </div>
          </section>
          <section aria-labelledby="vehicle-price">
            <h2 id="vehicle-price">Itemized price estimate</h2>
            <dl className={styles.price}>
              <div>
                <dt>Vehicle price</dt>
                <dd>{formatCurrency(snapshot.price.vehiclePrice)}</dd>
              </div>
              <div>
                <dt>Processing fee</dt>
                <dd>{formatCurrency(snapshot.price.processingFee)}</dd>
              </div>
              <div>
                <dt>Road tax</dt>
                <dd>{formatCurrency(snapshot.price.roadTax)}</dd>
              </div>
              <div>
                <dt>SST on processing fee ({snapshot.sstRate * 100}%)</dt>
                <dd>{formatCurrency(snapshot.price.serviceTax)}</dd>
              </div>
              <div>
                <dt>Estimated total</dt>
                <dd>{formatCurrency(snapshot.price.total)}</dd>
              </div>
            </dl>
            <p>
              Insurance and optional warranty are excluded. This portfolio estimate uses simulated
              financial terms.
            </p>
          </section>
          <VehicleFinancing key={id} total={snapshot.price.total} />
          <section aria-labelledby="vehicle-description">
            <h2 id="vehicle-description">About this vehicle</h2>
            <p className={styles.description}>{snapshot.description}</p>
          </section>
          <section aria-labelledby="vehicle-inspection">
            <h2 id="vehicle-inspection">Signed inspection checklist</h2>
            <p>
              {snapshot.checklist.grade ? `Grade ${snapshot.checklist.grade}` : "Ungraded"} · Signed{" "}
              {formatMalaysianDate(snapshot.checklist.signedAt)}
            </p>
            {Array.from(new Set(snapshot.checklist.points.map((point) => point.category))).map(
              (category) => (
                <details key={category} className={styles.checklist}>
                  <summary>{label(category)}</summary>
                  <ul>
                    {snapshot.checklist.points
                      .filter((point) => point.category === category)
                      .map((point) => (
                        <li key={point.code}>
                          <strong>
                            {point.code} · {point.description}
                          </strong>
                          <span>
                            {point.kind === null ? "No finding recorded" : label(point.kind)}
                            {point.severity ? ` · ${label(point.severity)}` : ""}
                          </span>
                          {point.estimatedRepairCost > 0 && (
                            <span>
                              Estimated repair cost: {formatCurrency(point.estimatedRepairCost)}
                            </span>
                          )}
                        </li>
                      ))}
                  </ul>
                </details>
              ),
            )}
          </section>
        </div>
      )}
    </section>
  );
}
