"use client";

import { Button } from "@/components/ui/Button";
import { useInitialToast } from "@/hooks/use-initial-toast";
import styles from "@/components/account/Account.module.css";

export default function AccountError({ reset }: { reset: () => void }) {
  useInitialToast("Your account could not be loaded. Try again when the service is available.");

  return (
    <section className={styles.error}>
      <h1 className={styles.errorTitle}>Your account</h1>
      <Button onClick={reset}>Try loading account again</Button>
      <a href="/login">Sign in</a>
    </section>
  );
}
