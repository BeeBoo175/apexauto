"use client";
import { useState, type ReactNode } from "react";
import { StaffLink } from "./StaffLink";
import { useStaffNavigation } from "@/hooks/use-staff-navigation";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { Button } from "@/components/ui/Button";
import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";
import styles from "./StaffFrame.module.css";
import {
  StaffActorContext,
  StaffNavigationLockContext,
  StaffHistoryContext,
} from "./StaffActorBoundary";

export function StaffFrame({ actor, children }: { actor: CurrentActor; children: ReactNode }) {
  const session = useIntakeActor(actor);
  const navigation = useStaffNavigation();
  const fitViewport = navigation.pathname === "/dashboard/intake";
  const logout = useAuthMutation("logout", "/login");
  const [mobileOpen, setMobileOpen] = useState(false);

  function navigate(event: { preventDefault: () => void }, href: string) {
    navigation.navigate(event, href);
    setMobileOpen(false);
  }
  if (!session.active) {
    return null;
  }
  return (
    <StaffActorContext.Provider value={actor}>
      <StaffNavigationLockContext.Provider value={navigation.lockedRef}>
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
              <StaffLink
                className={styles.publicLink}
                href="/"
                prefetch={false}
                onNavigate={(event) => navigate(event, "/")}
              >
                View public site
              </StaffLink>
            </div>
            <nav className={styles.navigation} aria-label="Staff navigation">
              {hasIntakeAccess(actor) && (
                <StaffLink
                  href="/dashboard/intake"
                  prefetch={false}
                  className={styles.activeLink}
                  aria-current="page"
                  onNavigate={(event) => navigate(event, "/dashboard/intake")}
                >
                  Intake appointments
                </StaffLink>
              )}
              {!hasIntakeAccess(actor) && (
                <StaffLink
                  href="/dashboard"
                  prefetch={false}
                  className={styles.activeLink}
                  aria-current="page"
                  onNavigate={(event) => navigate(event, "/dashboard")}
                >
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
          <div className={styles.content}>
            <StaffHistoryContext.Provider
              value={{ path: navigation.historyPath, complete: navigation.completeHistory }}
            >
              {children}
            </StaffHistoryContext.Provider>
          </div>
        </section>
      </StaffNavigationLockContext.Provider>
    </StaffActorContext.Provider>
  );
}
