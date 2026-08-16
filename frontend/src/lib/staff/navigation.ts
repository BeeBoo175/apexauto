import type { Metadata } from "next";
import { hasAccountAdministration } from "@/lib/accounts/staff-accounts";
import { hasIntakeAccess, type CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { hasSalesAccess } from "@/lib/sales/contracts";
import { hasSettingsAccess } from "@/lib/settings/settings";
import { hasPerformanceAccess } from "@/lib/performance/performance";
import { hasTestDriveAccess } from "@/lib/test-drives/staff";
import { hasListingAccess } from "@/lib/vehicles/listings";
import { hasWorkCalendarAccess } from "./work-calendar";

export interface StaffWorkspaceLink {
  href: string;
  label: string;
  // Intake and workshop own nested routes, so their link stays active below the base path.
  activeBelow?: boolean;
  allowed: (actor: CurrentActor) => boolean;
}

// Home gathers what needs the user across several pages, so it exists for the roles that work across several pages.
// Technicians land on their Workshop queue and administrators on Accounts instead.
export const hasHomeAccess = (actor: Pick<CurrentActor, "role">) =>
  hasPerformanceAccess(actor) || actor.role === "SalesAgent";

// Order is the sidebar order: the overview pages first (Home, the planning calendar, then Performance for
// managers), then the work pages in the order a vehicle moves through them. The Administration and fallback links
// are rendered by the frame itself.
export const staffWorkspaceLinks: StaffWorkspaceLink[] = [
  { href: "/dashboard/home", label: "Home", allowed: hasHomeAccess },
  {
    href: "/dashboard/work-calendar",
    label: "Work calendar",
    allowed: (actor) => hasWorkCalendarAccess(actor.role),
  },
  {
    href: "/dashboard/performance",
    label: "Performance",
    activeBelow: true,
    allowed: hasPerformanceAccess,
  },
  {
    href: "/dashboard/intake",
    label: "Intake",
    activeBelow: true,
    allowed: hasIntakeAccess,
  },
  {
    href: "/dashboard/workshop",
    label: "Workshop",
    activeBelow: true,
    allowed: hasInspectionAccess,
  },
  {
    href: "/dashboard/listings",
    label: "Listings",
    activeBelow: true,
    allowed: hasListingAccess,
  },
  {
    href: "/dashboard/test-drives",
    label: "Test drives",
    activeBelow: true,
    allowed: (actor) => hasTestDriveAccess(actor.role),
  },
  {
    href: "/dashboard/sales",
    label: "Sales",
    activeBelow: true,
    allowed: hasSalesAccess,
  },
  {
    href: "/dashboard/accounts",
    label: "Accounts",
    activeBelow: true,
    allowed: hasAccountAdministration,
  },
  {
    href: "/dashboard/settings",
    label: "Settings",
    activeBelow: true,
    allowed: hasSettingsAccess,
  },
];

export const staffPageTitles: Record<string, string> = {
  "/dashboard/profile": "Account",
  ...Object.fromEntries(staffWorkspaceLinks.map((link) => [link.href, link.label])),
};

// Workspaces that fill the viewport and scroll inside their own panes.
export const fitViewportPaths = staffWorkspaceLinks.map((link) => link.href);

export function staffWorkspacePath(pathname: string) {
  if (Object.hasOwn(staffPageTitles, pathname)) {
    return pathname;
  }
  return (
    staffWorkspaceLinks.find((link) => link.activeBelow && pathname.startsWith(`${link.href}/`))
      ?.href ?? pathname
  );
}

export function staffLinkActive(link: StaffWorkspaceLink, selectedPath: string) {
  return link.activeBelow ? selectedPath.startsWith(link.href) : selectedPath === link.href;
}

// The browser tab names the workspace the address names, so the sidebar, heading and tab never disagree.
export function workspaceMetadata(path: string): Metadata {
  return { title: staffPageTitles[path] };
}
