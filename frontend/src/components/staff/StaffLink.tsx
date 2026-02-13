"use client";

import { useContext, type ComponentProps } from "react";
import Link from "next/link";
import { StaffNavigationLockContext } from "./StaffActorBoundary";

export function StaffLink(props: ComponentProps<typeof Link>) {
  const lockedRef = useContext(StaffNavigationLockContext);
  return (
    <Link
      {...props}
      onNavigate={(event) => {
        if (lockedRef?.current) {
          event.preventDefault();
          return;
        }
        props.onNavigate?.(event);
      }}
    />
  );
}
