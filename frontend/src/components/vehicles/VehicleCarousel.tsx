import { ScrollStrip } from "@/components/ui/ScrollStrip";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { VehicleCard } from "./VehicleCard";
import { VehicleCardView } from "./VehicleCardView";
import styles from "./VehicleCarousel.module.css";

// A horizontal, scroll-snapped row of the canonical catalogue card. The shared scroll strip adds the
// edge fades and step chevrons that the dashboard tabs use, shown only on a side with more cars.
const SHELL_VEHICLE = {
  branchName: "Showroom name",
  make: "Make",
  model: "Model",
  trim: "Trim level",
  manufactureYear: 2024,
  mileageKilometres: 50000,
  transmission: "Automatic",
  fuel: "Petrol",
  grade: "A",
} as const;
const SHELL_COUNT = 4;

export function VehicleCarousel({
  vehicles,
  label,
  loading = false,
}: {
  vehicles: readonly PublicVehicle[];
  label: string;
  // Shows silhouettes of the card in place of the cars while they load.
  loading?: boolean;
}) {
  return (
    <div className={styles.carousel}>
      <ScrollStrip label={label} className={styles.strip} dragScroll>
        <ul className={styles.track}>
          {loading &&
            Array.from({ length: SHELL_COUNT }, (_, index) => (
              <li key={index} className={styles.slide}>
                <VehicleCardView
                  shell
                  vehicle={SHELL_VEHICLE}
                  price="RM 00,000.00"
                  monthly="RM 000.00"
                  photoUrl={null}
                  photoPending
                  first={false}
                  photoVersion={0}
                />
              </li>
            ))}
          {vehicles.map((vehicle) => (
            <li key={vehicle.id} className={styles.slide}>
              <VehicleCard vehicle={vehicle} first={false} photoVersion={0} />
            </li>
          ))}
        </ul>
      </ScrollStrip>
    </div>
  );
}
