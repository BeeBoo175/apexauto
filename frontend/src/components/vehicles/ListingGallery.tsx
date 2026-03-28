"use client";

import { useState } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/Button";
import styles from "./ListingGallery.module.css";

export function ListingGallery({
  vehicleId,
  photoIds,
  vehicleName,
}: {
  vehicleId: string;
  photoIds: string[];
  vehicleName: string;
}) {
  return (
    <section className={styles.gallery} aria-label="Saved listing photos">
      <h3>Listing photos</h3>
      {photoIds.length === 0 ? (
        <p>No photos have been selected for this saved draft.</p>
      ) : (
        <div className={styles.photos}>
          {photoIds.map((id, index) => (
            <ListingPhoto
              key={`${vehicleId}:${id}`}
              vehicleId={vehicleId}
              id={id}
              position={index + 1}
              vehicleName={vehicleName}
            />
          ))}
        </div>
      )}
    </section>
  );
}

function ListingPhoto({
  vehicleId,
  id,
  position,
  vehicleName,
}: {
  vehicleId: string;
  id: string;
  position: number;
  vehicleName: string;
}) {
  const [failed, setFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  function retry() {
    setFailed(false);
    setAttempt((current) => current + 1);
  }

  return (
    <figure className={styles.photo}>
      <div className={styles.image}>
        {failed ? (
          <div className={styles.recovery}>
            <p role="status">Photo {position} is unavailable.</p>
            <Button size="compact" variant="secondary" onClick={retry}>
              Retry photo {position}
            </Button>
          </div>
        ) : (
          <Image
            key={attempt}
            src={`/api/staff/listings/${vehicleId}/photos/${id}`}
            alt={`${vehicleName} · listing photo ${position}`}
            width={960}
            height={640}
            unoptimized
            loading={position === 1 ? "eager" : "lazy"}
            onError={() => setFailed(true)}
          />
        )}
      </div>
      <figcaption>{position === 1 ? "Cover photo" : `Photo ${position}`}</figcaption>
    </figure>
  );
}
