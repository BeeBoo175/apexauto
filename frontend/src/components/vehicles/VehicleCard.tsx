"use client";

import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { useVehicleCardPhotos } from "@/hooks/use-vehicle-card-photos";
import { formatCurrency } from "@/lib/currency-format";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { VehicleCardView } from "./VehicleCardView";
import styles from "./VehicleCardView.module.css";

export function VehicleCard({
  vehicle,
  first,
  photoVersion,
}: {
  vehicle: PublicVehicle;
  first: boolean;
  photoVersion: number;
}) {
  const photos = useVehicleCardPhotos(vehicle, photoVersion);
  const name = `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`;

  return (
    <div className={styles.interactive}>
      <Link
        href={`/vehicles/${vehicle.id}`}
        prefetch={false}
        className={styles.link}
        aria-label={`View details for ${name}`}
      >
        <VehicleCardView
          vehicle={vehicle}
          price={formatCurrency(vehicle.vehiclePrice)}
          photoUrl={photos.photoId ? `/api/vehicles/${vehicle.id}/photos/${photos.photoId}` : null}
          first={first || photos.position > 0}
          photoVersion={photoVersion}
        />
      </Link>
      {photos.count > 1 && (
        <div className={styles.photoNavigation}>
          <span className={styles.photoControl}>
            {photos.canPrevious && (
              <Button
                size="icon"
                variant="photo"
                aria-label={`Previous photo of ${name}`}
                disabled={photos.busy}
                onClick={() => void photos.navigate(-1)}
              >
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="12" cy="12" r="10" fill="currentColor" />
                  <path d="m14 7-5 5 5 5" stroke="white" strokeWidth="1.5" />
                </svg>
              </Button>
            )}
          </span>
          <span className={styles.photoControl}>
            {photos.canNext && (
              <Button
                size="icon"
                variant="photo"
                aria-label={`Next photo of ${name}`}
                disabled={photos.busy}
                onClick={() => void photos.navigate(1)}
              >
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="12" cy="12" r="10" fill="currentColor" />
                  <path d="m10 7 5 5-5 5" stroke="white" strokeWidth="1.5" />
                </svg>
              </Button>
            )}
          </span>
          <span className={styles.photoCount} role="status" aria-live="polite" aria-atomic="true">
            {photos.busy ? "Loading photos…" : `${photos.position + 1} / ${photos.count}`}
          </span>
        </div>
      )}
    </div>
  );
}
