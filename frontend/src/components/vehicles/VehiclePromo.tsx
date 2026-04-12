import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { useVehiclePromo } from "@/hooks/use-vehicle-promo";
import { promoCode } from "@/lib/vehicles/purchase-quote";
import styles from "./VehiclePurchaseSummary.module.css";

export function VehiclePromo({ promo }: { promo: ReturnType<typeof useVehiclePromo> }) {
  return (
    <form
      className={styles.promo}
      onSubmit={(event) => {
        event.preventDefault();
        void promo.apply();
      }}
    >
      <div className={styles.promoEntry}>
        <Input
          label="Promo code"
          id="vehicle-promo"
          value={promo.draft}
          maxLength={100}
          aria-describedby="vehicle-promo-help vehicle-promo-status"
          onChange={(event) => promo.changeCode(event.target.value)}
        />
        <Button type="submit" variant="secondary" disabled={promo.busy || !promoCode(promo.draft)}>
          {promo.busy ? "Checking…" : "Apply"}
        </Button>
      </div>
      <p id="vehicle-promo-help">Case-sensitive. Does not reserve the car or lock its price.</p>
      <div className={styles.promoFeedback}>
        <p id="vehicle-promo-status" role="status">
          {promo.accepted ? `${promo.accepted.promoCode} applied to the total below.` : ""}
        </p>
        {promo.accepted && (
          <Button type="button" size="compact" variant="ghost" onClick={promo.remove}>
            Remove
          </Button>
        )}
      </div>
    </form>
  );
}
