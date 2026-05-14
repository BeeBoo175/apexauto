"use client";

import { useContext, useState, type ComponentProps } from "react";
import Link, { useLinkStatus } from "next/link";
import { useRouter } from "next/navigation";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffNavigationLockContext } from "./StaffNavigationLockContext";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import styles from "./StaffLink.module.css";

export function StaffLink({
  prefetchOnIntent = false,
  ...props
}: ComponentProps<typeof Link> & { prefetchOnIntent?: boolean }) {
  const operationLock = useContext(StaffNavigationLockContext);
  const router = useRouter();
  const { blocked, message } = useGuardedAction();
  const navigation = useContext(StaffNavigationContext);
  const [intent, setIntent] = useState(false);
  let prefetch = props.prefetch;
  if (prefetchOnIntent) {
    prefetch = intent && !props["aria-current"] ? null : false;
  }

  function prepare() {
    if (prefetchOnIntent && !operationLock?.lockedRef.current && !props["aria-current"]) {
      setIntent(true);
    }
  }

  return (
    <Link
      {...props}
      className={`${styles.link} ${props.className ?? ""}`}
      prefetch={prefetch}
      aria-disabled={blocked || props["aria-disabled"]}
      title={blocked ? message : props.title}
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
        if (operationLock?.lockedRef.current) {
          event.preventDefault();
          operationLock.guard.request(() => {
            const href = typeof props.href === "string" ? props.href : props.href.pathname;
            if (!href) {
              return;
            }
            navigation?.recordNavigation(href);
            let prevented = false;
            props.onNavigate?.({
              preventDefault: () => {
                prevented = true;
              },
            });
            if (!prevented) {
              router[props.replace ? "replace" : "push"](href, { scroll: props.scroll });
            }
          });
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
