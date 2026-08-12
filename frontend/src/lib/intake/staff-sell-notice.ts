import { hasIntakeAccess, roleNames, type CurrentActor } from "@/lib/auth/actor";

export interface StaffSellNotice {
  message: string;
  href: string;
  label: string;
  // True when the role may book the trade-in itself by choosing the customer.
  assisted: boolean;
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
      message: `You are signed in as ${role}. Choose the customer this trade-in is for. The appointment appears in the Intake workspace and the customer is emailed the confirmation.`,
      href: "/dashboard/intake",
      label: "Open intake workspace",
      assisted: true,
    };
  }
  if (actor.role === "WorkshopTechnician") {
    return {
      message: `You are signed in as ${role}. Sales staff book seller appointments. Your inspections and repairs are in the Workshop workspace.`,
      href: "/dashboard/workshop",
      label: "Open workshop workspace",
      assisted: false,
    };
  }
  return {
    message: `You are signed in as ${role}. Administrator accounts manage accounts and settings and do not take intake bookings.`,
    href: "/dashboard/accounts",
    label: "Open account administration",
    assisted: false,
  };
}
