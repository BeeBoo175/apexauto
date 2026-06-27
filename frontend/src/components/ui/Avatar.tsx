"use client";

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
  const { person } = usePersonPreview(accountId, open);
  const pending = !person;
  // Every row keeps its slot while the shared read loads, so the card never changes size.
  const rows = {
    role: person ? roleNames[person.role] : detail,
    branch: person?.branchName ?? "",
    mobile: person?.mobile ?? "",
    email: person?.email ?? "",
  };

  return (
    <span className={styles.previewText}>
      <span className={styles.previewName}>{person?.name ?? name}</span>
      <span className={styles.previewRole}>{rows.role}</span>
      <span className={styles.previewRole} data-pending={pending}>
        {rows.branch}
      </span>
      <span className={styles.previewContact}>
        <span data-pending={pending}>{rows.mobile}</span>
        <span data-pending={pending}>{rows.email}</span>
      </span>
    </span>
  );
}

// With `detail` set the avatar shows a preview on hover, focus or tap, with contact details fetched once the
// preview rests open. `onOpen` makes the avatar and label one button, which the caller answers by showing the
// person in place, so nobody leaves their work. A touch tap shows the preview first and a second tap opens.
export function Avatar({
  accountId,
  name,
  pictureId,
  size = "md",
  detail,
  label,
  onOpen,
  nested = false,
}: {
  accountId: string;
  name: string;
  pictureId: string | null;
  size?: AvatarSize;
  detail?: string;
  // Visible text beside the picture; it is part of the button, so the whole label is clickable.
  label?: string;
  onOpen?: () => void;
  // Inside a link or clickable row: a plain element stands in for the button, which keeps the row's own link valid.
  nested?: boolean;
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

  function activate(event: React.SyntheticEvent) {
    event.stopPropagation();
    event.preventDefault();
    if (touchPointer && !previewOpen) {
      open();
      return;
    }
    setPreviewOpen(false);
    onOpen?.();
  }

  const face = (
    <>
      <AvatarFace accountId={accountId} name={name} pictureId={pictureId} size={size} />
      {label !== undefined && <span className={styles.label}>{label}</span>}
    </>
  );
  const behaviour = {
    className: styles.trigger,
    onFocus: open,
    onBlur: () => setPreviewOpen(false),
    onPointerDown: (event: React.PointerEvent) => setTouchPointer(event.pointerType === "touch"),
    onClick: activate,
    onKeyDown: (event: React.KeyboardEvent) => {
      if (event.key === "Escape") {
        setPreviewOpen(false);
      }
    },
  };

  return (
    <span
      ref={host}
      className={styles.previewHost}
      onMouseEnter={open}
      onMouseLeave={() => setPreviewOpen(false)}
    >
      {nested ? (
        <span
          {...behaviour}
          role="button"
          tabIndex={-1}
          aria-label={label === undefined ? `${name}, ${detail}` : undefined}
        >
          {face}
        </span>
      ) : (
        <button
          {...behaviour}
          type="button"
          aria-label={label === undefined ? `${name}, ${detail}` : undefined}
        >
          {face}
        </button>
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
