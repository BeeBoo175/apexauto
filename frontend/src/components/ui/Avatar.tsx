"use client";

import Link from "next/link";
import { useState } from "react";
import { usePersonPreview } from "@/hooks/use-person-preview";
import { roleNames } from "@/lib/auth/actor";
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

// The initials are the placeholder: they show while the private picture loads or when it fails,
// and the picture fades in over them. The picture is always requested through the authenticated route.
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
  const [loadedPicture, setLoadedPicture] = useState<string | null>(null);
  const [failedPicture, setFailedPicture] = useState<string | null>(null);
  const showPicture = pictureId !== null && failedPicture !== pictureId;

  return (
    <span className={`${styles.avatar} ${styles[size]}`} aria-hidden="true">
      {initialsOf(name)}
      {showPicture && (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          className={styles.picture}
          data-loaded={loadedPicture === pictureId}
          src={`/api/accounts/${accountId}/picture?v=${pictureId}`}
          alt=""
          loading="lazy"
          decoding="async"
          onLoad={() => setLoadedPicture(pictureId)}
          onError={() => setFailedPicture(pictureId)}
        />
      )}
    </span>
  );
}

function PreviewDetails({
  accountId,
  name,
  detail,
  open,
}: {
  accountId: string;
  name: string;
  detail: string;
  open: boolean;
}) {
  const person = usePersonPreview(accountId, open);
  const role = person ? roleNames[person.role] : null;
  const lines = person
    ? [[role, person.branchName].filter(Boolean).join(" · "), person.mobile, person.email]
    : [detail];

  return (
    <span className={styles.previewText}>
      <span className={styles.previewName}>{person?.name ?? name}</span>
      {lines.filter(Boolean).map((line) => (
        <span key={line} className={styles.previewDetail}>
          {line}
        </span>
      ))}
      {!person && <span className={styles.previewPending}>Loading contact details</span>}
    </span>
  );
}

// With `detail` set the avatar links to the person's profile and shows a preview on hover, focus
// or tap, with contact details fetched once the preview rests open. A touch tap opens the preview
// first and a second tap follows the link.
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
          <PreviewDetails accountId={accountId} name={name} detail={detail} open={previewOpen} />
        </span>
      ) : null}
    </span>
  );
}
