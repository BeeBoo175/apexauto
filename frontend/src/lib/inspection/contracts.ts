export const inspectionStatuses = ["All", "Unassigned", "Draft", "Sealed"] as const;
export const repairStatuses = [
  "All",
  "PendingApproval",
  "Approved",
  "ApprovalNotRequired",
  "Completed",
  "Superseded",
] as const;
export type InspectionRow = {
  vehicleId: string;
  branchId: string;
  branchName: string;
  registrationPlate: string;
  make: string;
  model: string;
  checkedInAt: string;
  vehicleStatus: string;
  inspectionId: string | null;
  technicianId: string | null;
  assignmentVersion: number | null;
  revision: number | null;
  status: (typeof inspectionStatuses)[number];
};
export type RepairRow = {
  requestId: string;
  inspectionId: string;
  vehicleId: string;
  branchId: string;
  branchName: string;
  registrationPlate: string;
  technicianId: string;
  assignmentVersion: number;
  revision: number;
  repairVersion: number;
  budget: number;
  requestedAt: string;
  approvalId: string | null;
  coveringApprovalId: string | null;
  completionId: string | null;
  status: (typeof repairStatuses)[number];
};
export type Technician = { id: string; identity: string };
