import type { PublicVehicle } from "@/lib/vehicles/contracts";
import layout from "./VehicleLayout.module.css";
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
  const specifications = [
    { label: "Make", value: vehicle.make },
    { label: "Model", value: vehicle.model },
    { label: "Variant", value: vehicle.trim },
    { label: "Year", value: String(vehicle.manufactureYear) },
    { label: "Mileage", value: `${vehicle.mileageKilometres.toLocaleString("en-MY")} km` },
    { label: "Transmission", value: vehicle.transmission },
    { label: "Fuel", value: vehicle.fuel },
    { label: "Showroom", value: vehicle.branchName },
  ];

  return (
    <section aria-labelledby={headingId}>
      <h2 id={headingId} className={layout.sectionHeading}>
        Overview
      </h2>
      <p className={styles.description}>{description}</p>
      <dl className={styles.specifications}>
        {specifications.map((row) => (
          <div key={row.label}>
            <dt>{row.label}</dt>
            <dd>{row.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
