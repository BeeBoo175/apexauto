"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { createPortal } from "react-dom";
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
  linked = true,
}: {
  accountId: string;
  name: string;
  pictureId: string | null;
  size?: AvatarSize;
  detail?: string;
  // False inside another link or clickable row: the preview still shows on hover, without a link.
  linked?: boolean;
}) {
  const [previewOpen, setPreviewOpen] = useState(false);
  const [touchPointer, setTouchPointer] = useState(false);
  const [anchor, setAnchor] = useState<PreviewAnchor | null>(null);
  const host = useRef<HTMLSpanElement>(null);

  if (detail === undefined) {
    return <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size={size} />;
  }

  function open() {
    if (host.current) {
      setAnchor(anchorOf(host.current));
    }
    setPreviewOpen(true);
  }

  const face = <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size={size} />;

  return (
    <span
      ref={host}
      className={styles.previewHost}
      onMouseEnter={open}
      onMouseLeave={() => setPreviewOpen(false)}
    >
      {linked ? (
        <Link
          className={styles.link}
          href={`/dashboard/people/${accountId}`}
          aria-label={`${name}, ${detail}`}
          onFocus={open}
          onBlur={() => setPreviewOpen(false)}
          onPointerDown={(event) => setTouchPointer(event.pointerType === "touch")}
          onClick={(event) => {
            event.stopPropagation();
            if (touchPointer && !previewOpen) {
              event.preventDefault();
              open();
            }
          }}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              setPreviewOpen(false);
            }
          }}
        >
          {face}
        </Link>
      ) : (
        face
      )}
      {previewOpen && anchor
        ? createPortal(
            <span
              className={styles.preview}
              role="tooltip"
              style={{
                left: anchor.left,
                top: anchor.top,
                transform: anchor.above ? "translateY(-100%)" : undefined,
              }}
            >
              <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size="md" />
              <PreviewDetails
                accountId={accountId}
                name={name}
                detail={detail}
                open={previewOpen}
              />
            </span>,
            anchor.container,
          )
        : null}
    </span>
  );
}

type PreviewAnchor = { left: number; top: number; above: boolean; container: HTMLElement };

const PREVIEW_WIDTH = 300;
const PREVIEW_HEIGHT = 140;

// The preview sits in a fixed layer beside the avatar, so table cells, queues and drawers cannot clip it.
// Inside a modal drawer it joins that dialog, since a modal dialog covers everything outside it.
function anchorOf(element: HTMLElement): PreviewAnchor {
  const rect = element.getBoundingClientRect();
  const above = rect.bottom + PREVIEW_HEIGHT > window.innerHeight && rect.top > PREVIEW_HEIGHT;
  return {
    left: Math.max(8, Math.min(rect.left, window.innerWidth - PREVIEW_WIDTH - 8)),
    top: above ? rect.top - 6 : rect.bottom + 6,
    above,
    container: element.closest("dialog") ?? document.body,
  };
}
