import Image from "next/image";
import Link from "next/link";
import tradeInPhoto from "../../../public/images/vehicle-trade-in.webp";
import { VehicleValuationFields } from "@/components/sell/VehicleValuationFields";
import { Button } from "@/components/ui/Button";
import buttonStyles from "@/components/ui/Button.module.css";
import { Icon } from "@/components/ui/Icon";
import type { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import { formatCurrency } from "@/lib/currency-format";
import financeStyles from "./VehicleFinancing.module.css";
import styles from "./VehicleTradeIn.module.css";
import layout from "./VehicleLayout.module.css";

export function VehicleTradeIn({ tradeIn }: { tradeIn: ReturnType<typeof useVehicleTradeIn> }) {
  const accepted = tradeIn.accepted;
  const support = (
    <footer className={styles.tradeInSupport}>
      <Link
        href="/buying-guide#trade-in"
        target="_blank"
        rel="noopener noreferrer"
        aria-label="How trade-in works (opens in a new tab)"
        className={financeStyles.guideLink}
      >
        How trade-in works <Icon name="next" />
      </Link>
      <p>Final value is subject to inspection.</p>
    </footer>
  );

  return (
    <section aria-labelledby="vehicle-trade-in" className={layout.columns}>
      <div className={styles.tradeInPhoto}>
        <Image
          src={tradeInPhoto}
          alt="A hand holding a car key beside a vehicle"
          sizes="(max-width: 700px) 100vw, 420px"
        />
      </div>
      <div className={styles.tradeInContent}>
        <header className={styles.tradeInIntroduction}>
          <h2 id="vehicle-trade-in">Trade-in valuation</h2>
          <p>Estimate your car’s value and apply it to financing.</p>
        </header>
        <form
          className={styles.tradeInForm}
          onSubmit={(event) => {
            event.preventDefault();
            void tradeIn.valueVehicle();
          }}
        >
          <div className={styles.modeStage}>
            <fieldset
              className={styles.editor}
              data-active={tradeIn.editing}
              disabled={!tradeIn.editing || tradeIn.busy}
              aria-hidden={!tradeIn.editing}
            >
              {tradeIn.taxonomy && (
                <>
                  <div className={styles.fields}>
                    <VehicleValuationFields
                      taxonomy={tradeIn.taxonomy}
                      value={tradeIn.draft}
                      onChange={tradeIn.changeDraft}
                    />
                  </div>
                  <Button type="submit" disabled={tradeIn.busy}>
                    {tradeIn.busy ? "Valuing trade-in…" : "Get my valuation"}
                  </Button>
                </>
              )}
              {!tradeIn.taxonomy && !tradeIn.optionsFailed && (
                <div
                  className={styles.optionsShell}
                  aria-busy="true"
                  aria-label="Loading vehicle options"
                >
                  <span aria-hidden="true" className={styles.optionPlaceholder} />
                  <span aria-hidden="true" className={styles.optionPlaceholder} />
                  <span aria-hidden="true" className={styles.optionPlaceholder} />
                  <span aria-hidden="true" className={styles.optionPlaceholder} />
                  <span aria-hidden="true" className={styles.optionPlaceholder} />
                </div>
              )}
              {!tradeIn.taxonomy && tradeIn.optionsFailed && (
                <p className={styles.optionsError} role="status">
                  Vehicle options are unavailable. Your financing estimate is still available.
                </p>
              )}
              {support}
            </fieldset>
            <section
              className={styles.result}
              data-active={!tradeIn.editing}
              aria-hidden={tradeIn.editing}
              aria-label="Trade-in valuation result"
            >
              <dl className={styles.valuation} role="status" aria-atomic="true">
                {accepted && (
                  <>
                    <dt className={styles.vehicle}>
                      {accepted.vehicle.manufactureYear} {accepted.vehicle.make}{" "}
                      {accepted.vehicle.model} {accepted.vehicle.trim} ·{" "}
                      {accepted.vehicle.mileageKilometres.toLocaleString("en-MY")} km
                    </dt>
                    <dd className={styles.range}>
                      <span>Valuation range ({accepted.quote.valuationYear})</span>
                      {formatCurrency(accepted.quote.minValuation)}–
                      {formatCurrency(accepted.quote.maxValuation)}
                    </dd>
                    <dt className={styles.allowanceLabel}>Applied to financing</dt>
                    <dd className={styles.allowanceValue}>
                      {formatCurrency(accepted.quote.minValuation)}
                    </dd>
                  </>
                )}
              </dl>
              <div className={styles.formAction}>
                <a
                  href="#financing"
                  className={`${buttonStyles.button} ${buttonStyles.primary} ${buttonStyles.md}`}
                  tabIndex={tradeIn.editing ? -1 : undefined}
                >
                  View updated financing <Icon name="next" />
                </a>
                <Button
                  type="button"
                  size="compact"
                  variant="ghost"
                  disabled={tradeIn.editing}
                  onClick={tradeIn.startNewValuation}
                >
                  Get a new valuation
                </Button>
              </div>
              {support}
            </section>
          </div>
          {tradeIn.optionsFailed && !tradeIn.busy && (
            <Button type="button" variant="secondary" onClick={tradeIn.retryOptions}>
              Retry vehicle options
            </Button>
          )}
        </form>
      </div>
    </section>
  );
}
