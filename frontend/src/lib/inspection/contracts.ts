export const inspectionStatuses = ["All", "Unassigned", "Draft", "Sealed"] as const;
export const repairStatuses = [
  "All",
  "PendingApproval",
  "Approved",
  "ApprovalNotRequired",
  "Completed",
  "Superseded",
] as const;
export const severities = [
  "Advisory",
  "MinorDefect",
  "MajorDefect",
  "CriticalDisqualifier",
] as const;
export type Severity = (typeof severities)[number];
export type Assessment = {
  estimatedRepairCost: number;
  grade: "A" | "B" | "C" | null;
  isRejected: boolean;
};
export type Point = { code: string; category: string; description: string };
export type Entry = {
  pointCode: string;
  kind: "Finding" | "NotFitted";
  severity: Severity | null;
  estimatedRepairCost: number;
  notes: string;
};
export type Save = {
  operationId: string;
  inspectionId: string;
  actorId: string;
  assignmentVersion: number;
  expectedVersion: number;
  version: number;
  catalogVersion: string;
  entries: Entry[];
  assessment: Assessment;
  recordedAt: string;
  photoIds: string[];
  examinedPointCodes: string[] | null;
};
export type DraftVehicle = {
  registrationPlate: string;
  make: string;
  model: string;
  vin: string;
  mileageKilometres: number;
  status: string;
  branchName: string;
};
export type Draft = {
  photoReferences: PhotoReference[];
  id: string;
  vehicleId: string;
  branchId: string;
  technicianId: string;
  assignmentVersion: number;
  createdAt: string;
  latestSave: Save | null;
  revision: number;
  isSigned: boolean;
  reopenedAfterSaveVersion: number;
  repairVersion: number;
  catalogVersion: string;
  catalog: Point[];
  vehicle: DraftVehicle;
};
export type PhotoReference = { id: string; pointCode: string };
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
export type QueueKind = "vehicles" | "repair-requests";
export type QueueFilters = {
  search: string;
  status: string;
  branchId: string;
  from: string;
  to: string;
  pageSize: number;
};
export type Technician = { id: string; identity: string };
