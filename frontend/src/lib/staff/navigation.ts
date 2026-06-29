import { hasAccountAdministration } from "@/lib/accounts/staff-accounts";
import { hasIntakeAccess, type CurrentActor } from "@/lib/auth/actor";
import { hasInspectionAccess } from "@/lib/inspection/contracts";
import { hasSalesAccess } from "@/lib/sales/contracts";
import { hasSettingsAccess } from "@/lib/settings/settings";
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

// Order is the sidebar order. The Administration and fallback links are rendered by the frame itself.
export const staffWorkspaceLinks: StaffWorkspaceLink[] = [
  {
    href: "/dashboard/work-calendar",
    label: "Work calendar",
    allowed: (actor) => hasWorkCalendarAccess(actor.role),
  },
  {
    href: "/dashboard/intake",
    label: "Intake appointments",
    activeBelow: true,
    allowed: hasIntakeAccess,
  },
  {
    href: "/dashboard/inspection",
    label: "Workshop",
    activeBelow: true,
    allowed: hasInspectionAccess,
  },
  { href: "/dashboard/management", label: "Listings", allowed: hasListingAccess },
  { href: "/dashboard/sales", label: "Sales", allowed: hasSalesAccess },
  {
    href: "/dashboard/test-drives",
    label: "Test drives",
    allowed: (actor) => hasTestDriveAccess(actor.role),
  },
  { href: "/dashboard/settings", label: "Settings", allowed: hasSettingsAccess },
  { href: "/dashboard/accounts", label: "Accounts", allowed: hasAccountAdministration },
];

export const staffPageTitles: Record<string, string> = {
  "/dashboard": "Administration",
  "/dashboard/account": "Account",
  "/dashboard/inspection/repairs": "Repair requests",
  ...Object.fromEntries(staffWorkspaceLinks.map((link) => [link.href, link.label])),
};

// Workspaces that fill the viewport and scroll inside their own panes.
export const fitViewportPaths = [
  "/dashboard/inspection/repairs",
  ...staffWorkspaceLinks.map((link) => link.href),
];

export function staffLinkActive(link: StaffWorkspaceLink, selectedPath: string) {
  return link.activeBelow ? selectedPath.startsWith(link.href) : selectedPath === link.href;
}
