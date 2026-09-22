import type { PublicVehicle } from "./contracts";

export function vehicleTitle(
  vehicle: Pick<PublicVehicle, "manufactureYear" | "make" | "model" | "trim">,
) {
  return `${vehicle.manufactureYear} ${vehicle.make} ${vehicle.model} ${vehicle.trim}`;
}
