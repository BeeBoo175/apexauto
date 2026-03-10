import type { EditorSnapshot } from "./editor-snapshot";

export const workshopTasks = ["Active", "Inspection", "Approval", "Repairs", "All"] as const;
export const technicianTasks = ["MyWork", "Waiting", "Claimable"] as const;
export type TechnicianTask = (typeof technicianTasks)[number];
export const workshopSorts = [
  "registrationPlate",
  "status",
  "technicianIdentity",
  "branchName",
  "budget",
  "since",
] as const;
export const workshopStatuses = [
  "AwaitingInspection",
  "InspectionUnderway",
  "RepairRequestRequired",
  "ApprovalRequired",
  "RepairsToComplete",
  "ReadyForSale",
  "Rejected",
] as const;

export type WorkshopRow = {
  vehicleId: string;
  branchId: string;
  branchName: string;
  registrationPlate: string;
  make: string;
  model: string;
  vehicleStatus: string;
  inspectionId: string | null;
  technicianId: string | null;
  technicianIdentity: string | null;
  revision: number | null;
  requestId: string | null;
  budget: number | null;
  since: string;
  dateSource: "CheckedIn" | "Requested";
  status: (typeof workshopStatuses)[number];
};

export type WorkshopJobs = {
  items: WorkshopRow[];
  total: number;
  pageSize: number;
  after: string | null;
  nextCursor: string | null;
};

const taskLabels: Record<string, string> = {
  Active: "Active work",
  Inspection: "Needs inspection",
  Approval: "Needs approval",
  Repairs: "Repairs to complete",
  All: "All vehicles",
};

const statusLabels: Record<WorkshopRow["status"], string> = {
  AwaitingInspection: "Awaiting inspection",
  InspectionUnderway: "Inspection underway",
  RepairRequestRequired: "Repair request needed",
  ApprovalRequired: "Budget approval needed",
  RepairsToComplete: "Repairs to complete",
  ReadyForSale: "Ready for sale",
  Rejected: "Rejected",
};

export const workshopTaskLabel = (value: string) => taskLabels[value] ?? value;
export const workshopStatusLabel = (value: WorkshopRow["status"]) => statusLabels[value];

export function workshopRowSection(row: WorkshopRow): "inspection" | "repairs" {
  return [
    "RepairRequestRequired",
    "ApprovalRequired",
    "RepairsToComplete",
    "ReadyForSale",
  ].includes(row.status)
    ? "repairs"
    : "inspection";
}

export function workshopSnapshotStatus(snapshot: EditorSnapshot): WorkshopRow["status"] {
  if (snapshot.draft.vehicle.status === "Available") {
    return "ReadyForSale";
  }
  if (snapshot.draft.vehicle.status === "Rejected") {
    return "Rejected";
  }
  if (!snapshot.draft.isSigned) {
    return "InspectionUnderway";
  }
  if (!snapshot.repairState || snapshot.repairState.status === "Completed") {
    return "RepairRequestRequired";
  }
  return snapshot.repairState.status === "PendingApproval"
    ? "ApprovalRequired"
    : "RepairsToComplete";
}
