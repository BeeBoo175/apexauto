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
import styles from "./StaffFrame.module.css";
import {
  StaffActorContext,
  StaffNavigationLockContext,
  StaffHistoryContext,
} from "./StaffActorBoundary";

export function StaffFrame({ actor, children }: { actor?: CurrentActor; children: ReactNode }) {
  const session = useIntakeActor(actor);
  const navigation = useStaffNavigation();
  const fitViewport = [
    "/dashboard/intake",
    "/dashboard/inspection",
    "/dashboard/inspection/repairs",
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
    <StaffActorContext.Provider value={actor ?? null}>
      <StaffNavigationLockContext.Provider value={navigation.lockedRef}>
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
              {(!actor || hasIntakeAccess(actor)) && (
                <StaffLink
                  href="/dashboard/intake"
                  prefetch={false}
                  className={
                    navigation.pathname.startsWith("/dashboard/intake")
                      ? styles.activeLink
                      : styles.navLink
                  }
                  aria-current={
                    navigation.pathname.startsWith("/dashboard/intake") ? "page" : undefined
                  }
                  onNavigate={(event) => navigate(event, "/dashboard/intake")}
                >
                  Intake appointments
                </StaffLink>
              )}
              {(!actor || hasInspectionAccess(actor)) &&
                [
                  {
                    href: "/dashboard/inspection",
                    title: "Inspections",
                    active:
                      navigation.pathname.startsWith("/dashboard/inspection") &&
                      navigation.pathname !== "/dashboard/inspection/repairs",
                  },
                  {
                    href: "/dashboard/inspection/repairs",
                    title: "Repair requests",
                    active: navigation.pathname === "/dashboard/inspection/repairs",
                  },
                ].map((item) => (
                  <StaffLink
                    key={item.href}
                    href={item.href}
                    prefetch={false}
                    className={item.active ? styles.activeLink : styles.navLink}
                    aria-current={item.active ? "page" : undefined}
                    onNavigate={(event) => navigate(event, item.href)}
                  >
                    {item.title}
                  </StaffLink>
                ))}
              {actor && !hasIntakeAccess(actor) && !hasInspectionAccess(actor) && (
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

function NavigationHistory({ capture }: { capture: () => void }) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  useEffect(() => {
    capture();
  }, [capture, pathname, searchParams]);
  return null;
}
