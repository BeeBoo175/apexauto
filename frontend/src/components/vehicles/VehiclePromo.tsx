import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import type { useVehiclePromo } from "@/hooks/use-vehicle-promo";
import { promoCode } from "@/lib/vehicles/purchase-quote";

export function VehiclePromo({ promo }: { promo: ReturnType<typeof useVehiclePromo> }) {
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void promo.apply();
      }}
    >
      <Input
        label="Promo code"
        id="vehicle-promo"
        value={promo.draft}
        maxLength={100}
        aria-describedby="vehicle-promo-help vehicle-promo-status"
        onChange={(event) => promo.changeCode(event.target.value)}
      />
      <p id="vehicle-promo-help">
        Codes are case-sensitive. Apply one code at a time. Validation does not reserve this vehicle
        or lock its price.
      </p>
      <Button type="submit" variant="secondary" disabled={promo.busy || !promoCode(promo.draft)}>
        {promo.busy ? "Checking promo…" : "Apply promo"}
      </Button>
      {promo.accepted && (
        <Button type="button" variant="secondary" onClick={promo.remove}>
          Remove promo
        </Button>
      )}
      <p id="vehicle-promo-status" role="status">
        {promo.accepted ? `Applied promo: ${promo.accepted.promoCode}` : "No promo applied"}
      </p>
    </form>
  );
}
