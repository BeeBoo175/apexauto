import type { Assessment, Point, Severity } from "./contracts";

export type ReportPhoto = {
  id: string;
  contentType: string;
  bytes: number;
  sha256: string;
  width: number;
  height: number;
};
export type ReportPoint = Point & {
  kind: "Finding" | "NotFitted" | null;
  severity: Severity | null;
  estimatedRepairCost: number;
  notes: string | null;
  photos: ReportPhoto[];
};
export type Report = {
  operationId: string;
  inspectionId: string;
  revision: number;
  branchId: string;
  saveId: string;
  saveVersion: number;
  assignmentVersion: number;
  vehicle: {
    id: string;
    intakeAppointmentId: string;
    intakeOwnerId: string;
    vin: string;
    registrationPlate: string;
    make: string;
    model: string;
    trim: string;
    manufactureYear: number;
    mileageKilometres: number;
    checkedInAt: string;
  };
  technicianId: string;
  technicianIdentity: string;
  catalogVersion: string;
  assessment: Assessment;
  checklist: ReportPoint[];
  signedAt: string;
};
export type RepairItem = { pointCode: string; work: string };
export type RepairRequest = {
  operationId: string;
  inspectionId: string;
  branchId: string;
  actorId: string;
  signedRevisionId: string;
  assignmentVersion: number;
  revision: number;
  expectedVersion: number;
  version: number;
  items: RepairItem[];
  budget: number;
  requestedAt: string;
};
export type Approval = {
  operationId: string;
  inspectionId: string;
  branchId: string;
  actorId: string;
  requestId: string;
  assignmentVersion: number;
  revision: number;
  repairVersion: number;
  approvedAt: string;
};
export type Completion = Omit<Approval, "approvedAt"> & {
  approvalId: string | null;
  notes: string;
  completedAt: string;
};
export type Reopening = {
  operationId: string;
  inspectionId: string;
  branchId: string;
  actorId: string;
  signedRevisionId: string;
  assignmentVersion: number;
  expectedRevision: number;
  revision: number;
  afterSaveVersion: number;
  reason: string;
  recordedAt: string;
};
export type Assignment = {
  operationId: string;
  inspectionId: string;
  vehicleId: string;
  branchId: string;
  actorId: string;
  technicianId: string;
  previousTechnicianId: string | null;
  expectedVersion: number;
  version: number;
  reason: string | null;
  recordedAt: string;
};
