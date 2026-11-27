"use client";

import Image from "next/image";
import { Icon } from "@/components/ui/Icon";
import { useKeptPhotos } from "@/hooks/use-kept-photos";
import { fuelLabel, type PublicVehicle } from "@/lib/vehicles/contracts";
import styles from "./VehicleCardView.module.css";

type CardVehicle = Pick<
  PublicVehicle,
  "branchName" | "make" | "model" | "trim" | "manufactureYear" | "mileageKilometres"
> & {
  transmission: string;
  fuel: string;
  // Staff previews of a draft listing have no signed grade to show yet.
  grade?: PublicVehicle["grade"];
};

export function VehicleCardView({
  vehicle,
  price,
  monthly = null,
  photoUrl,
  first,
  photoVersion,
  photoPending = false,
  shell = false,
}: {
  vehicle: CardVehicle;
  price: string;
  // The default-terms installment estimate; the staff preview has no confirmed price to estimate from.
  monthly?: string | null;
  photoUrl: string | null;
  first: boolean;
  photoVersion: number;
  photoPending?: boolean;
  // A loading silhouette: the real card laid out with stand-in text, so it has its final height.
  shell?: boolean;
}) {
  const { photos, shownUrl, failedAt, markLoaded, markFailed } = useKeptPhotos(
    photoUrl,
    photoPending,
  );

  return (
    <article
      className={styles.vehicle}
      data-shell={shell || undefined}
      aria-hidden={shell || undefined}
    >
      <div className={styles.photo}>
        {/* The blurred placeholder stays visible while the photo loads and when it cannot load. */}
        <span className={styles.photoPlaceholder} aria-hidden="true" />
        {photos.map((url) => {
          // Healthy photos keep their lifetime across confirmed reads; a failed one is retried after the next.
          const retry = url in failedAt && photoVersion > failedAt[url] ? photoVersion : 0;
          return (
            <Image
              key={`${url}:${retry}`}
              ref={(element) => {
                // A photo that finished loading before hydration never fires onLoad.
                if (element?.complete && element.naturalWidth > 0) {
                  markLoaded(url);
                }
              }}
              src={url}
              alt={url === shownUrl ? `${vehicle.make} ${vehicle.model} ${vehicle.trim}` : ""}
              width={960}
              height={640}
              unoptimized
              loading={first ? "eager" : "lazy"}
              data-shown={url === shownUrl || undefined}
              // The card's opening photo fades in over the placeholder; once browsing starts, photos swap at once.
              data-first={photos.length === 1 || undefined}
              onLoad={() => markLoaded(url)}
              onError={() => markFailed(url, photoVersion)}
            />
          );
        })}
        {!photoUrl && !photoPending && <p className="sr-only">Photography unavailable</p>}
      </div>
      <div className={styles.vehicleBody}>
        <div className={styles.eyebrowRow}>
          <p className={styles.eyebrow}>
            {vehicle.manufactureYear} · {vehicle.make}
          </p>
          {vehicle.grade && <span className={styles.grade}>Grade {vehicle.grade}</span>}
        </div>
        <h2 className={styles.title}>
          {vehicle.model} {vehicle.trim}
        </h2>
        <div className={styles.priceRow}>
          <p className={styles.price}>{price}</p>
          {monthly && (
            <p className={styles.monthly}>
              est. <strong>{monthly}</strong>/mo
            </p>
          )}
        </div>
        <dl className={styles.specStrip}>
          <div>
            <dt className={styles.specLabel}>Odometer</dt>
            <dd className={styles.specValue}>
              {vehicle.mileageKilometres.toLocaleString("en-MY")} km
            </dd>
          </div>
          <div>
            <dt className={styles.specLabel}>Gearbox</dt>
            <dd className={styles.specValue}>{vehicle.transmission}</dd>
          </div>
          <div>
            <dt className={styles.specLabel}>Fuel</dt>
            <dd className={styles.specValue}>{fuelLabel(vehicle.fuel)}</dd>
          </div>
        </dl>
      </div>
      <p className={styles.branchLine}>
        <Icon name="map-pin" />
        <span className={styles.branchName}>{vehicle.branchName}</span>
      </p>
    </article>
  );
}
