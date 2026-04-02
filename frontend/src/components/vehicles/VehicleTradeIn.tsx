import { VehicleValuationFields } from "@/components/sell/VehicleValuationFields";
import { Button } from "@/components/ui/Button";
import type { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import { formatCurrency } from "@/lib/currency-format";
import styles from "./VehicleFinancing.module.css";

export function VehicleTradeIn({ tradeIn }: { tradeIn: ReturnType<typeof useVehicleTradeIn> }) {
  const accepted = tradeIn.accepted;

  return (
    <section aria-labelledby="vehicle-trade-in">
      <h3 id="vehicle-trade-in">Indicative trade-in</h3>
      <p>
        The lower end of your valuation range reduces the estimated principal separately from cash.
        This is indicative and subject to inspection; it does not book an inspection or confirm an
        acquisition amount.
      </p>
      {!tradeIn.taxonomy && (
        <Button
          variant="secondary"
          disabled={tradeIn.busy}
          onClick={() => void tradeIn.loadOptions()}
        >
          {tradeIn.busy ? "Loading trade-in options…" : "Load trade-in options"}
        </Button>
      )}
      {tradeIn.taxonomy && (
        <form
          onSubmit={(event) => {
            event.preventDefault();
            void tradeIn.valueVehicle();
          }}
        >
          <div className={styles.fields}>
            <VehicleValuationFields
              taxonomy={tradeIn.taxonomy}
              value={tradeIn.draft}
              onChange={tradeIn.changeDraft}
            />
          </div>
          <Button type="submit" variant="secondary" disabled={tradeIn.busy}>
            {tradeIn.busy ? "Valuing trade-in…" : "Value trade-in"}
          </Button>
        </form>
      )}
      <div role="status" aria-atomic="true">
        {accepted ? (
          <>
            <p>
              Using valuation for {accepted.vehicle.manufactureYear} {accepted.vehicle.make}{" "}
              {accepted.vehicle.model} {accepted.vehicle.trim} at{" "}
              {accepted.vehicle.mileageKilometres.toLocaleString("en-MY")} km.
            </p>
            <p>
              Valuation range ({accepted.quote.valuationYear}):{" "}
              {formatCurrency(accepted.quote.minValuation)}–
              {formatCurrency(accepted.quote.maxValuation)}. Indicative allowance:{" "}
              {formatCurrency(accepted.quote.minValuation)}.
            </p>
            <p>Draft changes apply only after a successful valuation.</p>
          </>
        ) : (
          <p>No trade-in allowance applied</p>
        )}
      </div>
      {accepted && (
        <Button type="button" variant="secondary" onClick={tradeIn.remove}>
          Remove trade-in allowance
        </Button>
      )}
    </section>
  );
}
