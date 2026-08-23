"use client";

import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";

// Rendered inside the staff shell, so it stands alone: the public header offset and photograph do not apply here.
export function SessionCheckUnavailable({ status }: { status?: number }) {
  const busy = status === 429;
  return (
    <StatusPage
      code={busy ? "429" : "503"}
      icon="alert-triangle"
      tone="alert"
      title={busy ? "We're a little busy" : "We couldn't check your session"}
      actions={
        <Link href="/" className={buttonClassName("secondary")}>
          Back to home
        </Link>
      }
    >
      {busy
        ? "Too many requests reached us at once. You are still signed in. Wait a moment, then reload the page."
        : "We couldn't reach the service to confirm you are signed in. You are still signed in. Reload the page to try again."}
    </StatusPage>
  );
}
