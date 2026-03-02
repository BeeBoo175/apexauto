"use client";

import { useContext, useState, type ComponentProps } from "react";
import Link, { useLinkStatus } from "next/link";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext, StaffNavigationLockContext } from "./StaffActorBoundary";
import styles from "./StaffLink.module.css";

export function StaffLink({
  prefetchOnIntent = false,
  ...props
}: ComponentProps<typeof Link> & { prefetchOnIntent?: boolean }) {
  const lockedRef = useContext(StaffNavigationLockContext);
  const navigation = useContext(StaffNavigationContext);
  const [intent, setIntent] = useState(false);
  let prefetch = props.prefetch;
  if (prefetchOnIntent) {
    prefetch = intent && !props["aria-current"] ? null : false;
  }

  function prepare() {
    if (prefetchOnIntent && !lockedRef?.current && !props["aria-current"]) {
      setIntent(true);
    }
  }

  return (
    <Link
      {...props}
      className={`${styles.link} ${props.className ?? ""}`}
      prefetch={prefetch}
      onPointerEnter={(event) => {
        props.onPointerEnter?.(event);
        prepare();
      }}
      onPointerLeave={(event) => {
        props.onPointerLeave?.(event);
        setIntent(false);
      }}
      onFocus={(event) => {
        props.onFocus?.(event);
        prepare();
      }}
      onBlur={(event) => {
        props.onBlur?.(event);
        setIntent(false);
      }}
      onPointerDown={(event) => {
        props.onPointerDown?.(event);
        if (event.button === 0 && !event.defaultPrevented) {
          prepare();
        }
      }}
      onNavigate={(event) => {
        if (lockedRef?.current) {
          event.preventDefault();
          return;
        }
        const href = typeof props.href === "string" ? props.href : props.href.pathname;
        if (href) {
          navigation?.recordNavigation(href);
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
