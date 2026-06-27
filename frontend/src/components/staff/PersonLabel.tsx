"use client";

import { useState } from "react";
import { createPortal } from "react-dom";
import { Avatar } from "@/components/ui/Avatar";
import { PersonDialog } from "./PersonDialog";
import styles from "./PersonLabel.module.css";

// A staff member mentioned in running text or a detail: the avatar and the name, one button that opens the
// person's profile over the current work. Inside a link or clickable row it stays a plain element so the row's
// own link remains valid, and the hover preview works there too.
export function PersonLabel({
  id,
  name,
  identity,
  pictureId,
  nested = false,
}: {
  id: string;
  name: string;
  identity: string;
  pictureId: string | null;
  nested?: boolean;
}) {
  const [open, setOpen] = useState(false);

  return (
    <span className={styles.label}>
      <Avatar
        accountId={id}
        name={name}
        pictureId={pictureId}
        size="sm"
        detail={identity}
        label={name}
        nested={nested}
        onOpen={() => setOpen(true)}
      />
      {open &&
        // The dialog leaves the row's markup, so a click inside it can never activate the row's link, and its
        // events stop here so the row's handlers never hear them through the component tree either.
        createPortal(
          <div
            onClick={(event) => event.stopPropagation()}
            onPointerDown={(event) => event.stopPropagation()}
            onKeyDown={(event) => event.stopPropagation()}
          >
            <PersonDialog id={id} name={name} open={open} onClose={() => setOpen(false)} />
          </div>,
          document.body,
        )}
    </span>
  );
}
