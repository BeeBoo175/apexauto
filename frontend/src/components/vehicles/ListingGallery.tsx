"use client";

import { useState, type ReactNode } from "react";
import { ReloadNotice } from "@/components/ui/ReloadNotice";
import Image from "next/image";
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
  className = "",
  frameClassName = "",
  imageClassName = "",
  children,
}: {
  vehicleId: string;
  id: string;
  position: number;
  vehicleName: string;
  photoUrl?: string;
  label?: string;
  caption?: string;
  className?: string;
  frameClassName?: string;
  imageClassName?: string;

  children?: ReactNode;
}) {
  const [failed, setFailed] = useState(false);

  return (
    <figure className={`${styles.photo} ${className}`.trim()}>
      <div className={`${styles.image} ${frameClassName}`.trim()}>
        {failed ? (
          <div className={styles.recovery}>
            <ReloadNotice subject={label} />
          </div>
        ) : (
          <Image
            className={imageClassName}
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
      {caption && <figcaption>{caption}</figcaption>}
      {children}
    </figure>
  );
}
