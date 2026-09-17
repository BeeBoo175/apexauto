import { ScrollStrip } from "@/components/ui/ScrollStrip";
import type { PublicVehicle } from "@/lib/vehicles/contracts";
import { VehicleCard } from "./VehicleCard";
import styles from "./VehicleCarousel.module.css";

// A horizontal, scroll-snapped row of the canonical catalogue card. The shared scroll strip adds the
// edge fades and step chevrons that the dashboard tabs use, shown only on a side with more cars.
export function VehicleCarousel({
  vehicles,
  label,
}: {
  vehicles: readonly PublicVehicle[];
  label: string;
}) {
  return (
    <div className={styles.carousel}>
      <ScrollStrip label={label} className={styles.strip} dragScroll>
        <ul className={styles.track}>
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
