import { Icon } from "@/components/ui/Icon";
import type { Quote } from "@/lib/intake/contracts";
import { formatCurrency } from "@/lib/currency-format";
import type { VehicleDraft } from "@/lib/intake/vehicle-draft";
import styles from "./Sell.module.css";

export function VehicleRecord({
  vehicle,
  quote,
  photos,
}: {
  vehicle: VehicleDraft;
  quote: Quote | null;
  photos: number;
}) {
  return (
    <aside className={styles.summary} aria-label="Your vehicle record">
      <h2>Vehicle record</h2>
      <p className={vehicle.registrationPlate ? styles.plate : styles.platePending} translate="no">
        {vehicle.registrationPlate || "Registration pending"}
      </p>
      <p className={styles.vehicleName}>
        {[vehicle.manufactureYear, vehicle.make, vehicle.model].filter(Boolean).join(" ") ||
          "Your vehicle details"}
      </p>
      <dl className={styles.specs}>
        <div>
          <dt>Trim</dt>
          <dd>{vehicle.trim || "—"}</dd>
        </div>
        <div>
          <dt>Declared mileage</dt>
          <dd>
            {vehicle.mileageKilometres
              ? `${Number(vehicle.mileageKilometres).toLocaleString("en-MY")} km`
              : "—"}
          </dd>
        </div>
      </dl>
      <div className={styles.quote}>
        <h3>Indicative valuation</h3>
        {quote ? (
          <p className={styles.amount}>
            {formatCurrency(quote.minValuation, quote.currency)}
            <br />
            to {formatCurrency(quote.maxValuation, quote.currency)}
          </p>
        ) : (
          <p className={styles.help}>Complete your vehicle details to request a valuation.</p>
        )}
        <p className={styles.help}>
          This range is non-binding. The vehicle must be inspected before an offer can be confirmed.
        </p>
      </div>
      <div className={styles.recordFooter}>
        <Icon name="inspection" />
        <span>{photos} of 4 exterior photos confirmed</span>
      </div>
    </aside>
  );
}
