import { isRecord, isUuid, safeReturnTo } from "./contracts";

const roles = [
  "Customer",
  "SalesAgent",
  "BranchManager",
  "GeneralManager",
  "WorkshopTechnician",
  "Administrator",
] as const;
export type Role = (typeof roles)[number];
export interface CurrentActor {
  userId: string;
  role: Role;
  branchId: string | null;
}

export function parseActor(value: unknown): CurrentActor | null {
  if (!isRecord(value) || !isUuid(value.userId)) {
    return null;
  }
  const role = roles.find((item) => item === value.role);
  if (!role || (value.branchId !== null && !isUuid(value.branchId))) {
    return null;
  }
  const branchRole = ["SalesAgent", "BranchManager", "WorkshopTechnician"].includes(role);
  if (branchRole !== (value.branchId !== null)) {
    return null;
  }
  return { userId: value.userId, role, branchId: value.branchId };
}

export function hasIntakeAccess(actor: CurrentActor) {
  return ["SalesAgent", "BranchManager", "GeneralManager"].includes(actor.role);
}

export function actorDestination(actor: CurrentActor, returnTo?: string) {
  if (actor.role !== "Customer") {
    return hasIntakeAccess(actor) ? "/dashboard/intake" : "/dashboard";
  }
  const destination = safeReturnTo(returnTo);
  return destination.startsWith("/dashboard") ? "/account" : destination;
}

export function actorIdentity(actor: CurrentActor) {
  return `${actor.userId}:${actor.role}:${actor.branchId ?? ""}`;
}

export const roleNames: Record<Role, string> = {
  Customer: "Customer",
  SalesAgent: "Sales agent",
  BranchManager: "Branch manager",
  GeneralManager: "General manager",
  WorkshopTechnician: "Workshop technician",
  Administrator: "Administrator",
};
