"use client";

import { useState } from "react";
import Image from "next/image";
import { Icon } from "@/components/ui/Icon";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import styles from "./VehicleCardView.module.css";

type CardVehicle = Pick<
  PublicVehicle,
  "branchName" | "make" | "model" | "trim" | "manufactureYear" | "mileageKilometres"
> & {
  transmission: string;
  fuel: string;
};

export function VehicleCardView({
  vehicle,
  price,
  photoUrl,
  first,
  photoVersion,
  photoPending = false,
}: {
  vehicle: CardVehicle;
  price: string;
  photoUrl: string | null;
  first: boolean;
  photoVersion: number;
  photoPending?: boolean;
}) {
  const [failedAt, setFailedAt] = useState<number | null>(null);
  // Healthy photos keep their lifetime across confirmed reads.
  const photoKey = failedAt !== null && photoVersion > failedAt ? photoVersion : 0;

  return (
    <article className={styles.vehicle}>
      <div className={styles.photo}>
        <span className={styles.photoPlaceholder} aria-hidden="true" />
        {photoUrl && (
          <Image
            key={`${photoUrl}:${photoKey}`}
            src={photoUrl}
            alt={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
            width={960}
            height={640}
            unoptimized
            loading={first ? "eager" : "lazy"}
            onError={() => setFailedAt((current) => current ?? photoVersion)}
          />
        )}
        {!photoUrl && !photoPending && <p>Photography unavailable</p>}
      </div>
      <div className={styles.vehicleBody}>
        <div className={styles.priceRow}>
          <p className={styles.price}>{price}</p>
          <span className={styles.trustTag}>
            <Icon name="shield-check" /> 210-pt
          </span>
        </div>
        <div className={styles.identity}>
          <h2>
            {vehicle.manufactureYear} {vehicle.make} {vehicle.model} {vehicle.trim}
          </h2>
          <div className={styles.metadata}>
            <p>
              {vehicle.mileageKilometres.toLocaleString("en-MY")} km · {vehicle.transmission} ·{" "}
              {vehicle.fuel === "PlugInHybrid" ? "Plug-in hybrid" : vehicle.fuel}
            </p>
            <p className={styles.branchLine}>
              <Icon name="map-pin" />
              <span>{vehicle.branchName}</span>
            </p>
          </div>
        </div>
      </div>
    </article>
  );
}
