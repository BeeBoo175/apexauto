"use client";

import { useState } from "react";
import Image from "next/image";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import styles from "./Catalogue.module.css";

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
}: {
  vehicle: CardVehicle;
  price: string;
  photoUrl: string | null;
  first: boolean;
  photoVersion: number;
}) {
  const [failedAt, setFailedAt] = useState<number | null>(null);
  // Healthy photos keep their lifetime across confirmed reads.
  const photoKey = failedAt !== null && photoVersion > failedAt ? photoVersion : 0;

  return (
    <article className={styles.vehicle}>
      <div className={styles.photo}>
        {photoUrl ? (
          <Image
            key={photoKey}
            src={photoUrl}
            alt={`${vehicle.make} ${vehicle.model} ${vehicle.trim}`}
            width={960}
            height={640}
            unoptimized
            loading={first ? "eager" : "lazy"}
            onError={() => setFailedAt((current) => current ?? photoVersion)}
          />
        ) : (
          <p>Photography unavailable</p>
        )}
      </div>
      <div className={styles.vehicleBody}>
        <p className={styles.location}>{vehicle.branchName}</p>
        <h2>
          {vehicle.manufactureYear} {vehicle.make} {vehicle.model}
        </h2>
        <p>{vehicle.trim}</p>
        <p className={styles.specification}>
          {vehicle.mileageKilometres.toLocaleString("en-MY")} km · {vehicle.transmission} ·{" "}
          {vehicle.fuel === "PlugInHybrid" ? "Plug-in hybrid" : vehicle.fuel}
        </p>
        <p className={styles.price}>{price}</p>
        <p className={styles.help}>Vehicle price, before fees and road tax</p>
      </div>
    </article>
  );
}
