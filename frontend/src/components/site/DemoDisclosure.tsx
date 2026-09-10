"use client";

import { useSyncExternalStore } from "react";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import {
  acknowledgeDisclosure,
  readDisclosureState,
  readServerDisclosureState,
  subscribeToDisclosure,
} from "@/lib/demo/disclosure";
import styles from "./DemoDisclosure.module.css";

function keepOpen() {
  // Only the explicit button acknowledges the disclosure; Escape and backdrop do not.
}

export function DemoDisclosure() {
  const state = useSyncExternalStore(
    subscribeToDisclosure,
    readDisclosureState,
    readServerDisclosureState,
  );

  return (
    <Dialog
      open={state === "required"}
      onClose={keepOpen}
      title="This is a demonstration site"
      description="ApexAuto is a portfolio demo, not a real dealership."
      showCloseButton={false}
    >
      <ul className={styles.points}>
        <li>Use fictional customer, vehicle and transaction details only.</li>
        <li>
          You may deliberately use an email address you control to see real email delivery. Emails
          already delivered stay in their recipients&rsquo; inboxes.
        </li>
        <li>Application records can be cleared when the demo is deliberately reseeded.</li>
      </ul>
      <div className={styles.actions}>
        <Button type="button" onClick={acknowledgeDisclosure}>
          I understand
        </Button>
      </div>
    </Dialog>
  );
}
