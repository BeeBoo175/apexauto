"use client";

import { useState } from "react";
import Image from "next/image";
import { PhotoPlaceholder } from "@/components/ui/PhotoPlaceholder";
import { usePhotoLoading } from "@/hooks/use-photo-loading";
import styles from "./ReservationHistory.module.css";

// The reserved car's cover photograph in a list row. A photograph that cannot load leaves the placeholder.
export function ReservationThumbnail({
  reservationId,
  photoId,
}: {
  reservationId: string;
  photoId: string | null;
}) {
  const [failed, setFailed] = useState(false);
  const url =
    photoId && !failed ? `/api/account/reservations/${reservationId}/photos/${photoId}` : null;
  const photo = usePhotoLoading(url, () => setFailed(true));

  return (
    <span className={styles.thumb}>
      <PhotoPlaceholder loading={photo.loading} />
      {url && <Image src={url} alt="" width={96} height={64} unoptimized {...photo.imageProps} />}
    </span>
  );
}
