import { isRecord } from "@/lib/auth/contracts";

export interface VehicleDraft {
  make: string;
  model: string;
  trim: string;
  manufactureYear: string;
  mileageKilometres: string;
  registrationPlate: string;
}

export type ValuationDraft = Omit<VehicleDraft, "registrationPlate">;

export const emptyValuation: ValuationDraft = {
  make: "",
  model: "",
  trim: "",
  manufactureYear: "",
  mileageKilometres: "",
};

export const emptyVehicle: VehicleDraft = {
  ...emptyValuation,
  registrationPlate: "",
};

export function parseVehicleDraft(value: unknown): VehicleDraft | null {
  if (!isRecord(value)) {
    return null;
  }
  const fields = Object.keys(emptyVehicle);
  if (fields.some((key) => typeof value[key] !== "string" || value[key].length > 100)) {
    return null;
  }
  return {
    make: String(value.make),
    model: String(value.model),
    trim: String(value.trim),
    manufactureYear: String(value.manufactureYear),
    mileageKilometres: String(value.mileageKilometres),
    registrationPlate: String(value.registrationPlate),
  };
}
