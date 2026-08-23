"use client";

import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";

export default function AppError() {
  return (
    <StatusPage
      code="500"
      icon="alert-triangle"
      tone="alert"
      title="Something went wrong"
      actions={
        <Link href="/" className={buttonClassName("secondary")}>
          Back to home
        </Link>
      }
    >
      This page could not be loaded. Reload the page to try again.
    </StatusPage>
  );
}
