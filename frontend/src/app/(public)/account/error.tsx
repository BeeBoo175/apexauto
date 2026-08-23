"use client";

import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";

export default function AccountError() {
  return (
    <StatusPage
      code="503"
      icon="alert-triangle"
      tone="alert"
      title="Your account could not be loaded"
      actions={
        <Link href="/login" className={buttonClassName("secondary")}>
          Sign in
        </Link>
      }
    >
      The service did not answer. Reload the page to try again.
    </StatusPage>
  );
}
