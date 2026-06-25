"use client";

import Link from "next/link";
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
function AvatarFace({
  accountId,
  name,
  pictureId,
  size,
}: {
  accountId: string;
  name: string;
  pictureId: string | null;
  size: AvatarSize;
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

// With `detail` set the avatar links to the person's profile and shows a preview on hover, focus
// or tap. A touch tap opens the preview first and a second tap follows the link.
export function Avatar({
  accountId,
  name,
  pictureId,
  size = "md",
  detail,
}: {
  accountId: string;
  name: string;
  pictureId: string | null;
  size?: AvatarSize;
  detail?: string;
}) {
  const [previewOpen, setPreviewOpen] = useState(false);
  const [touchPointer, setTouchPointer] = useState(false);

  if (detail === undefined) {
    return <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size={size} />;
  }

  return (
    <span
      className={styles.previewHost}
      onMouseEnter={() => setPreviewOpen(true)}
      onMouseLeave={() => setPreviewOpen(false)}
    >
      <Link
        className={styles.link}
        href={`/dashboard/people/${accountId}`}
        aria-label={`${name}, ${detail}`}
        onFocus={() => setPreviewOpen(true)}
        onBlur={() => setPreviewOpen(false)}
        onPointerDown={(event) => setTouchPointer(event.pointerType === "touch")}
        onClick={(event) => {
          event.stopPropagation();
          if (touchPointer && !previewOpen) {
            event.preventDefault();
            setPreviewOpen(true);
          }
        }}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            setPreviewOpen(false);
          }
        }}
      >
        <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size={size} />
      </Link>
      {previewOpen ? (
        <span className={styles.preview} role="tooltip">
          <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size="md" />
          <span className={styles.previewText}>
            <span className={styles.previewName}>{name}</span>
            <span className={styles.previewDetail}>{detail}</span>
          </span>
        </span>
      ) : null}
    </span>
  );
}
