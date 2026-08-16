"use client";

import { useContext, useRef, type ComponentProps } from "react";
import Link, { useLinkStatus } from "next/link";
import { useRouter } from "next/navigation";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { StaffNavigationContext } from "./StaffActorBoundary";
import { StaffNavigationLockContext } from "./StaffNavigationLockContext";
import { useGuardedAction } from "@/hooks/use-guarded-action";
import styles from "./StaffLink.module.css";

// Staff links never prefetch from the viewport; only links that opt in prefetch, and only once the user shows intent.
const INTENT_PREFETCH_REPEAT_MS = 30_000;

export function StaffLink({
  prefetchOnIntent = false,
  ...props
}: ComponentProps<typeof Link> & { prefetchOnIntent?: boolean }) {
  const operationLock = useContext(StaffNavigationLockContext);
  const router = useRouter();
  const { blocked, message } = useGuardedAction();
  const navigation = useContext(StaffNavigationContext);
  const lastPrefetch = useRef(0);

  function destination() {
    return typeof props.href === "string" ? props.href : props.href.pathname;
  }

  // A ref keeps hover and focus from re-rendering every link in a list; router.prefetch is idempotent within the window.
  function prepare() {
    const href = destination();
    if (!prefetchOnIntent || !href || operationLock?.guard.isLocked() || props["aria-current"]) {
      return;
    }
    if (Date.now() - lastPrefetch.current < INTENT_PREFETCH_REPEAT_MS) {
      return;
    }
    lastPrefetch.current = Date.now();
    router.prefetch(href);
  }

  return (
    <Link
      {...props}
      className={`${styles.link} ${props.className ?? ""}`}
      prefetch={props.prefetch ?? false}
      aria-disabled={blocked || props["aria-disabled"]}
      title={blocked ? message : props.title}
      onPointerEnter={(event) => {
        props.onPointerEnter?.(event);
        prepare();
      }}
      onFocus={(event) => {
        props.onFocus?.(event);
        prepare();
      }}
      onPointerDown={(event) => {
        props.onPointerDown?.(event);
        if (event.button === 0 && !event.defaultPrevented) {
          prepare();
        }
      }}
      onNavigate={(event) => {
        if (operationLock?.guard.isLocked()) {
          event.preventDefault();
          operationLock.guard.request(() => {
            const href = destination();
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
        const href = destination();
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
