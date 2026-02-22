import workbench from "./AppointmentWorkbench.module.css";
import styles from "./AppointmentLoadingShell.module.css";

const appointmentDetails = [
  "Branch",
  "Customer",
  "Inspection",
  "Booked vehicle",
  "Declared mileage",
  "Reference",
];

export function AppointmentLoadingShell({
  label = "Loading appointment…",
  details = appointmentDetails,
}: {
  label?: string;
  details?: readonly string[];
}) {
  return (
    <div>
      <p role="status" className="sr-only">
        {label}
      </p>
      <div className={workbench.workbench} aria-hidden="true">
        <div className={workbench.recordHeading}>
          <span className={`${styles.placeholder} ${styles.registration}`} />
          <span className={`${styles.placeholder} ${styles.status}`} />
        </div>
        <dl className={workbench.details}>
          {details.map((label) => (
            <div key={label}>
              <dt>{label}</dt>
              <dd>
                <span
                  className={`${styles.placeholder} ${label === "Reference" ? styles.reference : styles.value}`}
                />
              </dd>
            </div>
          ))}
        </dl>
      </div>
    </div>
  );
}
