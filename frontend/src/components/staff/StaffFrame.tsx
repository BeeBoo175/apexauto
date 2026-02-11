"use client";
import { useState, type ReactNode } from "react";
import Link from "next/link";
import { useStaffViewTransitions } from "@/hooks/use-staff-view-transitions";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { Button } from "@/components/ui/Button";
import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";
import styles from "./StaffFrame.module.css";

export function StaffFrame({
  actor,
  children,
  fitViewport = false,
}: {
  actor: CurrentActor;
  children: ReactNode;
  fitViewport?: boolean;
}) {
  const session = useIntakeActor(actor);
  useStaffViewTransitions();
  const logout = useAuthMutation("logout", "/login");
  const [mobileOpen, setMobileOpen] = useState(false);
  if (!session.active) {
    return null;
  }
  return (
    <section
      className={`${styles.page} ${fitViewport ? styles.viewport : ""}`}
      inert={!session.ready}
    >
      <div className={styles.mobileBar}>
        <span className={styles.brand}>
          Apex<span>Auto</span>
        </span>
        <Button
          variant="shell"
          size="compact"
          aria-expanded={mobileOpen}
          aria-controls="staff-navigation"
          onClick={() => setMobileOpen(!mobileOpen)}
        >
          {mobileOpen ? "Close workspaces" : "Workspaces"}
        </Button>
      </div>
      <aside
        id="staff-navigation"
        className={`${styles.sidebar} ${mobileOpen ? styles.sidebarOpen : ""}`}
        aria-label="Dealership operations"
      >
        <div className={styles.identity}>
          <span className={`${styles.brand} ${styles.desktopBrand}`}>
            Apex<span>Auto</span>
          </span>
          <StaffLink className={styles.publicLink} href="/">
            View public site
          </StaffLink>
        </div>
        <nav className={styles.navigation} aria-label="Staff navigation">
          {hasIntakeAccess(actor) && (
            <StaffLink href="/dashboard/intake" className={styles.activeLink} aria-current="page">
              Intake appointments
            </StaffLink>
          )}
          {!hasIntakeAccess(actor) && (
            <StaffLink href="/dashboard" className={styles.activeLink} aria-current="page">
              Your staff workspace
            </StaffLink>
          )}
        </nav>
        <div className={styles.session}>
          <span>{roleNames[actor.role]}</span>
          <Button
            variant="shell"
            size="compact"
            disabled={logout.busy}
            onClick={() => void logout.submit()}
          >
            {logout.uncertain ? "Retry sign out" : "Sign out"}
          </Button>
        </div>
      </aside>
      <div className={styles.content}>{children}</div>
    </section>
  );
}

function StaffLink({
  href,
  children,
  className,
  "aria-current": currentPage,
}: {
  href: string;
  children: ReactNode;
  className?: string;
  "aria-current"?: "page";
}) {
  // Replace the document so prior actor-scoped router snapshots cannot be reused.
  return (
    <Link
      href={href}
      className={className}
      aria-current={currentPage}
      prefetch={false}
      onNavigate={(event) => {
        event.preventDefault();
        window.location.assign(href);
      }}
    >
      {children}
    </Link>
  );
}
