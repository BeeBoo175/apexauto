"use client";
import { Suspense, useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import { Logo } from "@/components/ui/Logo";
import { usePathname, useSearchParams } from "next/navigation";
import { StaffLink } from "./StaffLink";
import { StaffClientEntryBoundary } from "./StaffClientEntryBoundary";
import { useStaffNavigation } from "@/hooks/use-staff-navigation";
import { useIntakeActor } from "@/hooks/use-intake-actor";
import { useAuthMutation } from "@/hooks/use-auth-mutation";
import { usePersonPreview } from "@/hooks/use-person-preview";
import { Avatar } from "@/components/ui/Avatar";
import { Button } from "@/components/ui/Button";
import { CloseButton } from "@/components/ui/CloseButton";
import { Icon } from "@/components/ui/Icon";
import { EditGuardFeedback } from "@/components/ui/EditGuardFeedback";
import { Dialog } from "@/components/ui/Dialog";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { hasSettingsAccess } from "@/lib/settings/settings";
import { staffLinkActive, staffWorkspaceLinks } from "@/lib/staff/navigation";
import styles from "./StaffFrame.module.css";
import { StaffActorContext, StaffNavigationContext } from "./StaffActorBoundary";
import { StaffNavigationLockContext } from "./StaffNavigationLockContext";

const COLLAPSE_STORAGE_KEY = "apexauto_staff_nav_collapsed";
const COLLAPSE_CHANGE_EVENT = "apexauto_nav_collapse_change";

function subscribeToCollapse(callback: () => void) {
  window.addEventListener("storage", callback);
  window.addEventListener(COLLAPSE_CHANGE_EVENT, callback);
  return () => {
    window.removeEventListener("storage", callback);
    window.removeEventListener(COLLAPSE_CHANGE_EVENT, callback);
  };
}

function getCollapseSnapshot(): boolean {
  try {
    return localStorage.getItem(COLLAPSE_STORAGE_KEY) === "true";
  } catch {
    return false;
  }
}

function getCollapseServerSnapshot(): boolean {
  return false;
}

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
  const logout = useAuthMutation("logout", "/login");
  // The signed-in person's name and picture come from the shared person read, once per session.
  const { person } = usePersonPreview(actor?.userId ?? "", Boolean(actor));
  const [mobileOpen, setMobileOpen] = useState(false);
  const collapsed = useSyncExternalStore(
    subscribeToCollapse,
    getCollapseSnapshot,
    getCollapseServerSnapshot,
  );

  useEffect(() => {
    if (!mobileOpen) {
      return;
    }
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setMobileOpen(false);
      }
    }
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [mobileOpen]);

  function toggleCollapsed() {
    try {
      const next = !getCollapseSnapshot();
      localStorage.setItem(COLLAPSE_STORAGE_KEY, next ? "true" : "false");
      window.dispatchEvent(new Event(COLLAPSE_CHANGE_EVENT));
    } catch {
      // Storage access may fail in private browsing or constrained environments.
    }
  }

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
          <section className={styles.page} inert={Boolean(actor) && !ready}>
            <Suspense fallback={null}>
              <NavigationHistory capture={navigation.captureHistory} />
            </Suspense>
            <LoadingProgress label="Opening workspace" active={navigation.pending} />
            <div className={styles.mobileBar}>
              <StaffLink
                href="/"
                prefetch={false}
                className={styles.brand}
                aria-label="ApexAuto public site"
                onNavigate={(event) => navigate(event, "/")}
              >
                <Logo />
              </StaffLink>
              <Button
                variant="shell"
                size="icon"
                className={styles.mobileMenuButton}
                aria-label="Workspaces"
                title="Workspaces"
                aria-expanded={mobileOpen}
                aria-controls="staff-navigation"
                disabled={!actor}
                onClick={() => setMobileOpen(true)}
              >
                <Icon name="menu" />
              </Button>
            </div>
            {mobileOpen && (
              <div
                className={styles.backdrop}
                onClick={() => setMobileOpen(false)}
                aria-hidden="true"
              />
            )}
            <aside
              id="staff-navigation"
              className={`${styles.sidebar} ${mobileOpen ? styles.sidebarOpen : ""} ${collapsed ? styles.sidebarCollapsed : ""}`}
              aria-label="Dealership operations"
            >
              <div className={styles.identity}>
                <div className={styles.identityHeader}>
                  <StaffLink
                    href="/"
                    prefetch={false}
                    className={`${styles.brand} ${styles.desktopBrand}`}
                    aria-label="ApexAuto public site"
                    onNavigate={(event) => navigate(event, "/")}
                  >
                    <Logo />
                  </StaffLink>
                  <StaffLink
                    href="/"
                    prefetch={false}
                    className={styles.monogram}
                    aria-label="ApexAuto public site"
                    title={collapsed ? "ApexAuto public site" : undefined}
                    data-tooltip={collapsed ? "ApexAuto public site" : undefined}
                    onNavigate={(event) => navigate(event, "/")}
                  >
                    <Logo markOnly />
                  </StaffLink>
                  <StaffLink
                    href="/"
                    prefetch={false}
                    className={`${styles.brand} ${styles.mobileDrawerBrand}`}
                    aria-label="ApexAuto public site"
                    onNavigate={(event) => navigate(event, "/")}
                  >
                    <Logo />
                  </StaffLink>
                  <CloseButton
                    label="Close workspaces"
                    variant="shell"
                    className={styles.mobileDrawerClose}
                    onClick={() => setMobileOpen(false)}
                  />
                  <Button
                    variant="shell"
                    size="icon"
                    className={styles.collapseToggle}
                    aria-expanded={!collapsed}
                    aria-controls="staff-navigation"
                    aria-label={collapsed ? "Expand navigation" : "Collapse navigation"}
                    title={collapsed ? "Expand navigation" : "Collapse navigation"}
                    data-tooltip={collapsed ? "Expand navigation" : undefined}
                    onClick={toggleCollapsed}
                  >
                    <Icon name={collapsed ? "sidebar-expand" : "sidebar-collapse"} />
                  </Button>
                </div>
              </div>
              <nav className={styles.navigation} aria-label="Staff navigation">
                {staffWorkspaceLinks
                  .filter((link) => link.allowed(actor))
                  .map((link) => {
                    const active = staffLinkActive(link, navigation.selectedPath);
                    return (
                      <StaffLink
                        key={link.href}
                        href={link.href}
                        prefetch={false}
                        prefetchOnIntent={true}
                        className={active ? styles.activeLink : styles.navLink}
                        aria-current={active ? "page" : undefined}
                        aria-label={link.label}
                        title={collapsed ? link.label : undefined}
                        data-tooltip={collapsed ? link.label : undefined}
                        onNavigate={(event) => navigate(event, link.href)}
                      >
                        <Icon name={link.icon} />
                        <span className={styles.navLabel}>{link.label}</span>
                      </StaffLink>
                    );
                  })}
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
                      aria-label="Your staff workspace"
                      title={collapsed ? "Your staff workspace" : undefined}
                      data-tooltip={collapsed ? "Your staff workspace" : undefined}
                      onNavigate={(event) => navigate(event, "/dashboard")}
                    >
                      <Icon name="car" />
                      <span className={styles.navLabel}>Your staff workspace</span>
                    </StaffLink>
                  )}
              </nav>
              {actor && (
                <div className={styles.session}>
                  <StaffLink
                    href="/dashboard/profile"
                    prefetch={false}
                    className={styles.profile}
                    aria-label="Account"
                    title={collapsed ? (person?.name ?? "Account") : undefined}
                    data-tooltip={collapsed ? (person?.name ?? "Account") : undefined}
                    onNavigate={(event) => navigate(event, "/dashboard/profile")}
                  >
                    <Avatar
                      accountId={actor.userId}
                      name={person?.name ?? roleNames[actor.role]}
                      pictureId={person?.pictureId ?? null}
                      size="sm"
                    />
                    <span className={styles.profileText}>
                      <strong>{person?.name ?? "Your account"}</strong>
                      <span>{roleNames[actor.role]}</span>
                    </span>
                  </StaffLink>
                  <Button
                    variant="shell"
                    size="icon"
                    className={styles.logoutButton}
                    aria-label={logout.uncertain ? "Retry sign out" : "Sign out"}
                    title={
                      logout.uncertain
                        ? "Retry sign out"
                        : navigation.guardState.message || "Sign out"
                    }
                    data-tooltip={collapsed ? "Sign out" : undefined}
                    disabled={logout.busy || navigation.guardState.blocked}
                    onClick={() => {
                      navigation.operationLock.guard.request(
                        () => void logout.submit(),
                        "Discard and sign out",
                      );
                    }}
                  >
                    <Icon name="logout" />
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
