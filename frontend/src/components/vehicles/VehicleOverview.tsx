import type { PublicVehicle } from "@/lib/vehicles/contracts";
import styles from "./VehicleOverview.module.css";

type OverviewVehicle = Pick<
  PublicVehicle,
  "make" | "model" | "trim" | "manufactureYear" | "mileageKilometres" | "branchName"
> & {
  transmission: string;
  fuel: string;
};

export function VehicleOverview({
  vehicle,
  description,
  headingId = "vehicle-overview",
}: {
  vehicle: OverviewVehicle;
  description: string;
  headingId?: string;
}) {
  return (
    <section aria-labelledby={headingId}>
      <h2 id={headingId}>About this vehicle</h2>
      <p className={styles.description}>{description}</p>
      <dl className={styles.specifications}>
        <div>
          <dt>Make</dt>
          <dd>{vehicle.make}</dd>
        </div>
        <div>
          <dt>Model</dt>
          <dd>{vehicle.model}</dd>
        </div>
        <div>
          <dt>Variant</dt>
          <dd>{vehicle.trim}</dd>
        </div>
        <div>
          <dt>Year</dt>
          <dd>{vehicle.manufactureYear}</dd>
        </div>
        <div>
          <dt>Mileage</dt>
          <dd>{vehicle.mileageKilometres.toLocaleString("en-MY")} km</dd>
        </div>
        <div>
          <dt>Transmission</dt>
          <dd>{vehicle.transmission}</dd>
        </div>
        <div>
          <dt>Fuel</dt>
          <dd>{vehicle.fuel}</dd>
        </div>
        <div>
          <dt>Showroom</dt>
          <dd>{vehicle.branchName}</dd>
        </div>
      </dl>
    </section>
  );
}
