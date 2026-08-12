"use client";

import { Button } from "@/components/ui/Button";
import { useInitialToast } from "@/hooks/use-initial-toast";
import styles from "./SessionCheckUnavailable.module.css";

// Rendered inside the staff shell, so it stands alone: the public header offset and photograph do not apply here.
export function SessionCheckUnavailable({ status }: { status?: number }) {
  useInitialToast(
    status === 429
      ? "The service is busy. Your browser session has been kept. Try again shortly."
      : "Your session could not be checked. Your browser session has been kept. Try again before continuing.",
  );
  return (
    <section className={styles.page} aria-labelledby="session-check-title">
      <div className={styles.panel}>
        <div className={styles.heading}>
          <h1 id="session-check-title">Check your session</h1>
        </div>
        <Button onClick={() => window.location.reload()}>Try checking session again</Button>
      </div>
    </section>
  );
}
