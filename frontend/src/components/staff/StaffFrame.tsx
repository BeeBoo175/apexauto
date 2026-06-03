"use client";
import { Suspense, useEffect, useState, type ReactNode } from "react";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLink } from "./StaffLink";
import { StaffClientEntryBoundary } from "./StaffClientEntryBoundary";
import { useStaffNavigation } from "@/hooks/use-staff-navigation";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { Button } from "@/components/ui/Button";
import { CloseButton } from "@/components/ui/CloseButton";
import { EditGuardFeedback } from "@/components/ui/EditGuardFeedback";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { hasSalesAccess } from "@/lib/sales/contracts";
import { hasSettingsAccess } from "@/lib/settings/settings";
import { hasTestDriveAccess } from "@/lib/test-drives/staff";
import { hasWorkCalendarAccess } from "@/lib/staff/work-calendar";
import styles from "./StaffFrame.module.css";
import { StaffActorContext, StaffNavigationContext } from "./StaffActorBoundary";
import { StaffNavigationLockContext } from "./StaffNavigationLockContext";

export function StaffFrame({ actor, children }: { actor: CurrentActor; children: ReactNode }) {
  const session = useIntakeActor(actor);
  if (!session.active) {
    return null;
  }
  return (
    <StaffClientEntryBoundary>
      <StaffFrameContent actor={actor} ready={session.ready}>
        {children}
      </StaffFrameContent>
    </StaffClientEntryBoundary>
  );
}

function StaffFrameContent({
  actor,
  children,
  ready,
}: {
  actor: CurrentActor;
  children: ReactNode;
  ready: boolean;
}) {
  const navigation = useStaffNavigation();
  const fitViewport = [
    "/dashboard/intake",
    "/dashboard/inspection",
    "/dashboard/inspection/repairs",
    "/dashboard/management",
    "/dashboard/settings",
    "/dashboard/sales",
    "/dashboard/test-drives",
    "/dashboard/work-calendar",
  ].includes(navigation.pathname);
  const logout = useAuthMutation("logout", "/login");
  const [mobileOpen, setMobileOpen] = useState(false);

  function navigate(event: { preventDefault: () => void }, href: string) {
    navigation.navigate(event, href);
    setMobileOpen(false);
  }
  return (
    <StaffNavigationContext.Provider
      value={{
        pendingPath:
          navigation.selectedPath === navigation.pathname ? null : navigation.selectedPath,
        entryPath: navigation.entryPath,
        recordNavigation: navigation.recordNavigation,
      }}
    >
      <StaffActorContext.Provider value={actor ?? null}>
        <StaffNavigationLockContext.Provider value={navigation.operationLock}>
          <EditGuardFeedback guard={navigation.operationLock.guard} surface={Dialog} />
          <section
            className={`${styles.page} ${fitViewport ? styles.viewport : ""}`}
            inert={Boolean(actor) && !ready}
          >
            <Suspense fallback={null}>
              <NavigationHistory capture={navigation.captureHistory} />
            </Suspense>
            <LoadingProgress label="Opening workspace" active={navigation.pending} />
            <div className={styles.mobileBar}>
              <span className={styles.brand}>
                Apex<span>Auto</span>
              </span>
              {mobileOpen ? (
                <CloseButton
                  label="Close workspaces"
                  variant="shell"
                  aria-expanded
                  aria-controls="staff-navigation"
                  disabled={!actor}
                  onClick={() => setMobileOpen(false)}
                />
              ) : (
                <Button
                  variant="shell"
                  size="compact"
                  aria-expanded={false}
                  aria-controls="staff-navigation"
                  disabled={!actor}
                  onClick={() => setMobileOpen(true)}
                >
                  Workspaces
                </Button>
              )}
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
                {hasWorkCalendarAccess(actor.role) && (
                  <StaffLink
                    href="/dashboard/work-calendar"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath === "/dashboard/work-calendar"
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath === "/dashboard/work-calendar" ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/work-calendar")}
                  >
                    Work calendar
                  </StaffLink>
                )}
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
                {actor && hasSalesAccess(actor) && (
                  <StaffLink
                    href="/dashboard/sales"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath === "/dashboard/sales"
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath === "/dashboard/sales" ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/sales")}
                  >
                    Sales
                  </StaffLink>
                )}
                {actor && hasTestDriveAccess(actor.role) && (
                  <StaffLink
                    href="/dashboard/test-drives"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath === "/dashboard/test-drives"
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath === "/dashboard/test-drives" ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/test-drives")}
                  >
                    Test drives
                  </StaffLink>
                )}
                {actor && hasSettingsAccess(actor) && (
                  <StaffLink
                    href="/dashboard/settings"
                    prefetch={false}
                    prefetchOnIntent={true}
                    className={
                      navigation.selectedPath === "/dashboard/settings"
                        ? styles.activeLink
                        : styles.navLink
                    }
                    aria-current={
                      navigation.selectedPath === "/dashboard/settings" ? "page" : undefined
                    }
                    onNavigate={(event) => navigate(event, "/dashboard/settings")}
                  >
                    Settings
                  </StaffLink>
                )}
                {actor &&
                  !hasIntakeAccess(actor) &&
                  !hasInspectionAccess(actor) &&
                  !hasListingAccess(actor) &&
                  !hasSettingsAccess(actor) && (
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
                    disabled={logout.busy || navigation.guardState.blocked}
                    title={navigation.guardState.message || undefined}
                    onClick={() => {
                      navigation.operationLock.guard.request(
                        () => void logout.submit(),
                        "Discard and sign out",
                      );
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
