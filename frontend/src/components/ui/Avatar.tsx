"use client";

import { useState } from "react";
import styles from "./Avatar.module.css";

export type AvatarSize = "sm" | "md" | "lg";

export function initialsOf(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
}

// The picture is private, so it is always requested through the authenticated account route.
export function Avatar({
  accountId,
  name,
  pictureId,
  size = "md",
}: {
  accountId: string;
  name: string;
  pictureId: string | null;
  size?: AvatarSize;
}) {
  const [failedPicture, setFailedPicture] = useState<string | null>(null);
  const showPicture = pictureId !== null && failedPicture !== pictureId;

  return (
    <span className={`${styles.avatar} ${styles[size]}`} aria-hidden="true">
      {showPicture ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          className={styles.picture}
          src={`/api/accounts/${accountId}/picture?v=${pictureId}`}
          alt=""
          loading="lazy"
          decoding="async"
          onError={() => setFailedPicture(pictureId)}
        />
      ) : (
        initialsOf(name)
      )}
    </span>
  );
}
