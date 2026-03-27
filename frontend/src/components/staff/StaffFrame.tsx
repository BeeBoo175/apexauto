"use client";
import { Suspense, useEffect, useState, type ReactNode } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLink } from "./StaffLink";
import { useStaffNavigation } from "@/hooks/use-staff-navigation";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { Button } from "@/components/ui/Button";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { hasListingAccess } from "@/lib/vehicles/listings";
import styles from "./StaffFrame.module.css";
import {
  StaffActorContext,
  StaffNavigationLockContext,
  StaffNavigationContext,
} from "./StaffActorBoundary";

export function StaffFrame({ actor, children }: { actor?: CurrentActor; children: ReactNode }) {
  const session = useIntakeActor(actor);
  const navigation = useStaffNavigation();
  const fitViewport = [
    "/dashboard/intake",
    "/dashboard/inspection",
    "/dashboard/inspection/repairs",
    "/dashboard/management",
  ].includes(navigation.pathname);
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
    <StaffNavigationContext.Provider
      value={{
        pendingPath:
          navigation.selectedPath === navigation.pathname ? null : navigation.selectedPath,
        historyPath: navigation.historyPath,
        entryPath: navigation.entryPath,
        completeHistory: navigation.completeHistory,
        recordNavigation: navigation.recordNavigation,
      }}
    >
      <StaffActorContext.Provider value={actor ?? null}>
        <StaffNavigationLockContext.Provider value={navigation.operationLock}>
          <section
            className={`${styles.page} ${fitViewport ? styles.viewport : ""}`}
            inert={Boolean(actor) && !session.ready}
          >
            <Suspense fallback={null}>
              <NavigationHistory capture={navigation.captureHistory} />
            </Suspense>
            <LoadingProgress
              label="Opening workspace"
              active={navigation.pending || Boolean(navigation.historyPath)}
            />
            <div className={styles.mobileBar}>
              <span className={styles.brand}>
                Apex<span>Auto</span>
              </span>
              <Button
                variant="shell"
                size="compact"
                aria-expanded={mobileOpen}
                aria-controls="staff-navigation"
                disabled={!actor}
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
                {actor && hasIntakeAccess(actor) && (
                  <StaffLink
                    href="/dashboard/intake"
                    prefetch={false}
                    prefetchOnIntent={Boolean(actor)}
                    className={
                      navigation.selectedPath.startsWith("/dashboard/intake")
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath.startsWith("/dashboard/intake") ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/intake")}
                  >
                    Intake appointments
                  </StaffLink>
                )}
                {actor && hasInspectionAccess(actor) && (
                  <StaffLink
                    href="/dashboard/inspection"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath.startsWith("/dashboard/inspection")
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath.startsWith("/dashboard/inspection")
                        ? "page"
                        : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/inspection")}
                  >
                    Workshop
                  </StaffLink>
                )}
                {actor && hasListingAccess(actor) && (
                  <StaffLink
                    href="/dashboard/management"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath === "/dashboard/management"
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath === "/dashboard/management" ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/management")}
                  >
                    Listings
                  </StaffLink>
                )}
                {actor &&
                  !hasIntakeAccess(actor) &&
                  !hasInspectionAccess(actor) &&
                  !hasListingAccess(actor) && (
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
              {actor && (
                <div className={styles.session}>
                  <span>{roleNames[actor.role]}</span>
                  <Button
                    variant="shell"
                    size="compact"
                    disabled={logout.busy}
                    onPointerDown={(event) => {
                      if (navigation.lockedRef.current) {
                        event.preventDefault();
                      }
                    }}
                    onClick={() => {
                      if (!navigation.lockedRef.current) {
                        void logout.submit();
                      }
                    }}
                  >
                    {logout.uncertain ? "Retry sign out" : "Sign out"}
                  </Button>
                </div>
              )}
            </aside>
            <div
              className={styles.content}
              inert={navigation.pending && navigation.selectedPath !== navigation.pathname}
            >
              {children}
            </div>
          </section>
        </StaffNavigationLockContext.Provider>
      </StaffActorContext.Provider>
    </StaffNavigationContext.Provider>
  );
}

function NavigationHistory({ capture }: { capture: () => void }) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  useEffect(() => {
    capture();
  }, [capture, pathname, searchParams]);
  return null;
}
