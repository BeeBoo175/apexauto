"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/Button";
import styles from "./ListingGallery.module.css";

export function ListingGallery({
  vehicleId,
  photoIds,
  vehicleName,
  controls,
}: {
  vehicleId: string;
  photoIds: string[];
  vehicleName: string;
  controls?: (id: string, index: number) => ReactNode;
}) {
  return (
    <section className={styles.gallery} aria-label="Selected listing photos">
      {photoIds.length === 0 ? (
        <p>No photos are selected for this draft.</p>
      ) : (
        <div className={styles.photos}>
          {photoIds.map((id, index) => (
            <ListingPhoto
              key={`${vehicleId}:${id}`}
              vehicleId={vehicleId}
              id={id}
              position={index + 1}
              vehicleName={vehicleName}
            >
              {controls?.(id, index)}
            </ListingPhoto>
          ))}
        </div>
      )}
    </section>
  );
}

export function ListingPhoto({
  vehicleId,
  id,
  position,
  vehicleName,
  photoUrl,
  label = `Photo ${position}`,
  caption = position === 1 ? "Cover photo" : `Photo ${position}`,
  children,
}: {
  vehicleId: string;
  id: string;
  position: number;
  vehicleName: string;
  photoUrl?: string;
  label?: string;
  caption?: string;
  children?: ReactNode;
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
            <p role="status">{label} is unavailable.</p>
            <Button size="compact" variant="secondary" onClick={retry}>
              Retry {label.toLowerCase()}
            </Button>
          </div>
        ) : (
          <Image
            key={attempt}
            src={photoUrl ?? `/api/staff/listings/${vehicleId}/photos/${id}`}
            alt={`${vehicleName} · listing ${label.toLowerCase()}`}
            width={960}
            height={640}
            unoptimized
            loading={position === 1 ? "eager" : "lazy"}
            onError={() => setFailed(true)}
          />
        )}
      </div>
      <figcaption>{caption}</figcaption>
      {children}
    </figure>
  );
}
