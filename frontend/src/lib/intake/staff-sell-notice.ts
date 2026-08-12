import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";

export interface StaffSellNotice {
  message: string;
  href: string;
  label: string;
}

// Trade-in bookings on the public page belong to customers, so each staff role is pointed to the workspace
// where their own role may act on a seller's intake, or told plainly when their role has none.
export function staffSellNotice(actor: CurrentActor): StaffSellNotice | null {
  if (actor.role === "Customer") {
    return null;
  }
  const role = roleNames[actor.role];
  if (hasIntakeAccess(actor)) {
    return {
      message: `You are signed in as ${role}. Trade-in appointments are booked from a customer account. To record an intake for a seller, use the Intake workspace.`,
      href: "/dashboard/intake",
      label: "Open intake workspace",
    };
  }
  if (actor.role === "WorkshopTechnician") {
    return {
      message: `You are signed in as ${role}. Sales staff book seller appointments. Your inspections and repairs are in the Workshop workspace.`,
      href: "/dashboard/workshop",
      label: "Open workshop workspace",
    };
  }
  return {
    message: `You are signed in as ${role}. Administrator accounts manage accounts and settings and do not take intake bookings.`,
    href: "/dashboard/accounts",
    label: "Open account administration",
  };
}
