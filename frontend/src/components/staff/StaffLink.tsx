"use client";

import { useContext, type ComponentProps } from "react";
import Link, { useLinkStatus } from "next/link";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationLockContext } from "./StaffActorBoundary";
import styles from "./StaffLink.module.css";

export function StaffLink(props: ComponentProps<typeof Link>) {
  const lockedRef = useContext(StaffNavigationLockContext);
  return (
    <Link
      {...props}
      className={`${styles.link} ${props.className ?? ""}`}
      onNavigate={(event) => {
        if (lockedRef?.current) {
          event.preventDefault();
          return;
        }
        props.onNavigate?.(event);
      }}
    >
      {props.children}
      <LinkProgress />
    </Link>
  );
}

function LinkProgress() {
  const { pending } = useLinkStatus();
  return <LoadingProgress label="Opening workspace" active={pending} />;
}
