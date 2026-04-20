import styles from "./VehicleTableCells.module.css";

export function VehicleRegistration({ registration }: { registration: string }) {
  return <span className={styles.registration}>{registration}</span>;
}

export function VehicleDescription({
  vehicle,
}: {
  vehicle: { manufactureYear: number; make: string; model: string; trim: string };
}) {
  const identity = `${vehicle.make} ${vehicle.model}`;
  const specification = `${vehicle.manufactureYear} · ${vehicle.trim}`;
  return (
    <div className={styles.vehicle}>
      <span className={styles.identity} title={identity}>
        {identity}
      </span>
      <span className={styles.specification} title={specification}>
        {specification}
      </span>
    </div>
  );
}
