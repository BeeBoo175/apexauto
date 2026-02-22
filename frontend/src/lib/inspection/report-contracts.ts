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
