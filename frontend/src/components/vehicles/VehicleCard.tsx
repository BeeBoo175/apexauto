"use client";

import { Button } from "@/components/ui/Button";
import { useVehicleCardPhotos } from "@/hooks/use-vehicle-card-photos";
import { formatCurrency } from "@/lib/currency-format";
import { defaultMonthlyInstallment } from "@/lib/vehicles/financing";
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
  const installment = defaultMonthlyInstallment(vehicle.vehiclePrice);

  return (
    <div className={styles.interactive}>
      <a
        href={`/vehicles/${vehicle.id}`}
        className={styles.link}
        aria-label={`View details for ${name}`}
      >
        <VehicleCardView
          vehicle={vehicle}
          price={formatCurrency(vehicle.vehiclePrice)}
          monthly={
            installment === null ? null : `RM ${Math.round(installment).toLocaleString("en-MY")}`
          }
          photoUrl={photos.photoId ? `/api/vehicles/${vehicle.id}/photos/${photos.photoId}` : null}
          photoPending={photos.position > 0 && !photos.photoId}
          first={first || photos.position > 0}
          photoVersion={photoVersion}
        />
      </a>
      {photos.count > 1 && (
        <div className={styles.photoNavigation}>
          <span className={styles.photoControl}>
            {photos.canPrevious && (
              <Button
                size="icon"
                variant="photo"
                aria-label={`Previous photo of ${name}`}
                onClick={() => void photos.navigate(-1)}
              >
                <svg width="30" height="30" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="12" cy="12" r="11" fill="#f2f5f8" fillOpacity="0.9" />
                  <path d="m13.5 8-4 4 4 4" stroke="#485766" strokeWidth="1.75" />
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
                onClick={() => void photos.navigate(1)}
              >
                <svg width="30" height="30" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                  <circle cx="12" cy="12" r="11" fill="#f2f5f8" fillOpacity="0.9" />
                  <path d="m10.5 8 4 4-4 4" stroke="#485766" strokeWidth="1.75" />
                </svg>
              </Button>
            )}
          </span>
          <span className={styles.photoProgress} aria-hidden="true">
            {Array.from({ length: photos.count }, (_, index) => (
              <span
                key={index}
                className={styles.progressSegment}
                data-active={index === photos.position || undefined}
              />
            ))}
          </span>
          <span className="sr-only" role="status" aria-live="polite" aria-atomic="true">
            Photo {photos.position + 1} of {photos.count}
          </span>
        </div>
      )}
    </div>
  );
}
