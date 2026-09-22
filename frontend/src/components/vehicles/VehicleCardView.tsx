"use client";

import { useState } from "react";
import Image from "next/image";
import { Icon } from "@/components/ui/Icon";
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
  const [failedAt, setFailedAt] = useState<number | null>(null);
  const [loadedKey, setLoadedKey] = useState<string | null>(null);
  // Healthy photos keep their lifetime across confirmed reads.
  const photoKey = failedAt !== null && photoVersion > failedAt ? photoVersion : 0;
  const imageKey = `${photoUrl}:${photoKey}`;

  return (
    <article
      className={styles.vehicle}
      data-shell={shell || undefined}
      aria-hidden={shell || undefined}
    >
      <div className={styles.photo}>
        {/* The blurred placeholder stays visible while the photo loads and when it cannot load. */}
        <span className={styles.photoPlaceholder} aria-hidden="true" />
        {photoUrl && (
          <Image
            key={imageKey}
            ref={(element) => {
              // A photo that finished loading before hydration never fires onLoad.
              if (element?.complete && element.naturalWidth > 0) {
                setLoadedKey(imageKey);
              }
            }}
            src={photoUrl}
            alt={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
            width={960}
            height={640}
            unoptimized
            loading={first ? "eager" : "lazy"}
            data-loaded={loadedKey === imageKey || undefined}
            onLoad={() => setLoadedKey(imageKey)}
            onError={() => setFailedAt((current) => current ?? photoVersion)}
          />
        )}
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
            <dt>Odometer</dt>
            <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
          </div>
          <div>
            <dt>Gearbox</dt>
            <dd>{vehicle.transmission}</dd>
          </div>
          <div>
            <dt>Fuel</dt>
            <dd>{fuelLabel(vehicle.fuel)}</dd>
          </div>
        </dl>
      </div>
      <p className={styles.branchLine}>
        <Icon name="map-pin" />
        <span>{vehicle.branchName}</span>
      </p>
    </article>
  );
}
