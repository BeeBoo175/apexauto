"use client";

import { formatCurrency } from "@/lib/currency-format";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { VehicleCardView } from "./VehicleCardView";

export function VehicleCard({
  vehicle,
  first,
  photoVersion,
}: {
  vehicle: PublicVehicle;
  first: boolean;
  photoVersion: number;
}) {
  return (
    <VehicleCardView
      vehicle={vehicle}
      price={formatCurrency(vehicle.vehiclePrice)}
      photoUrl={
        vehicle.coverPhotoId ? `/api/vehicles/${vehicle.id}/photos/${vehicle.coverPhotoId}` : null
      }
      first={first}
      photoVersion={photoVersion}
    />
  );
}
