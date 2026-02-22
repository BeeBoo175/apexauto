export const inspectionStatuses = ["All", "Unassigned", "Draft", "Sealed"] as const;
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
export type Technician = { id: string; identity: string };
