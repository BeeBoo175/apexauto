"use client";

import { useState, type ReactNode } from "react";
import Image from "next/image";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { ListingPhoto } from "./ListingGallery";
import styles from "./VehicleDetailGallery.module.css";

export interface VehicleDetailPhoto {
  id: string;
  url: string;
}

export function VehicleDetailGallery({
  vehicleId,
  name,
  photos,
  badge,
}: {
  vehicleId: string;
  name: string;
  photos: VehicleDetailPhoto[];
  badge?: ReactNode;
}) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [failedThumbnails, setFailedThumbnails] = useState(() => new Set<string>());
  const [thumbnailAttempts, setThumbnailAttempts] = useState<Record<string, number>>({});
  const selectedIndex = Math.max(
    0,
    photos.findIndex((photo) => photo.id === selectedId),
  );
  const selected = photos[selectedIndex];

  function retryThumbnail(id: string) {
    if (!failedThumbnails.has(id)) {
      return;
    }
    setFailedThumbnails((current) => {
      const remaining = new Set(current);
      remaining.delete(id);
      return remaining;
    });
    setThumbnailAttempts((current) => ({ ...current, [id]: (current[id] ?? 0) + 1 }));
  }

  if (!selected) {
    return <div className={styles.empty}>Photography unavailable</div>;
  }

  return (
    <section aria-label="Vehicle photos" className={styles.gallery}>
      <div className={styles.selected}>
        <ListingPhoto
          key={`${vehicleId}:${selected.id}`}
          vehicleId={vehicleId}
          id={selected.id}
          position={selectedIndex + 1}
          vehicleName={name}
          photoUrl={selected.url}
          caption=""
          className={styles.selectedPhoto}
          frameClassName={styles.selectedFrame}
          imageClassName={styles.selectedImage}
        />
        {badge && <div className={styles.badge}>{badge}</div>}
      </div>
      {photos.length > 1 && (
        <div className={styles.thumbnails} aria-label="Choose a vehicle photo">
          {photos.map((photo, index) => (
            <Button
              key={photo.id}
              type="button"
              variant="ghost"
              className={styles.thumbnail}
              aria-label={`Show photo ${index + 1}`}
              aria-pressed={photo.id === selected.id}
              onClick={() => {
                setSelectedId(photo.id);
                retryThumbnail(photo.id);
              }}
            >
              {failedThumbnails.has(photo.id) ? (
                <span className={styles.thumbnailUnavailable}>
                  <Icon name="car" />
                </span>
              ) : (
                <Image
                  key={`${photo.id}:${thumbnailAttempts[photo.id] ?? 0}`}
                  src={photo.url}
                  alt=""
                  width={120}
                  height={80}
                  unoptimized
                  loading="lazy"
                  onError={() => setFailedThumbnails((current) => new Set(current).add(photo.id))}
                />
              )}
            </Button>
          ))}
        </div>
      )}
    </section>
  );
}
