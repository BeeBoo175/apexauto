import Link from "next/link";
import type { ReactNode } from "react";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import type { useVehicleTradeIn } from "@/hooks/use-vehicle-trade-in";
import { formatCurrency } from "@/lib/currency-format";
import { VehicleValuationFields } from "./VehicleValuationFields";
import styles from "./TradeInValuation.module.css";

interface TradeInValuationProps {
  tradeIn: ReturnType<typeof useVehicleTradeIn>;
  headingId?: string;
  heading?: string;
  description?: string;
  allowanceLabel?: string;
  primaryResultAction?: ReactNode;
  unavailableNotice?: string;
  className?: string;
}

const TRADE_IN_HIGHLIGHTS = [
  {
    icon: "inspection" as const,
    title: "Instant MYR range",
    note: "Calculated from make, model, year, and mileage.",
  },
  {
    icon: "car" as const,
    title: "Syncs to financing",
    note: "Applies your lower bound straight to loan calculations.",
  },
  {
    icon: "calendar" as const,
    title: "Zero-obligation check",
    note: "Confirm during a 90-minute branch inspection bay.",
  },
] as const;

export function TradeInValuation({
  tradeIn,
  headingId = "vehicle-trade-in",
  heading = "Trade-in valuation",
  description = "Estimate your car’s value and apply it to financing.",
  allowanceLabel = "Applied to financing",
  primaryResultAction,
  unavailableNotice = "Vehicle options are unavailable. Your financing estimate is still available.",
  className = "",
}: TradeInValuationProps) {
  const accepted = tradeIn.accepted;
  const optionsUnavailable = !tradeIn.taxonomy && tradeIn.optionsFailed;
  const support = (
    <div className={styles.tradeInSupport}>
      <p>Final value is subject to inspection.</p>
    </div>
  );

  const defaultResultAction = (
    <a
      href="#financing"
      className={buttonClassName("primary", "md")}
      tabIndex={tradeIn.editing ? -1 : undefined}
    >
      View updated financing <Icon name="next" />
    </a>
  );

  return (
    <section aria-labelledby={headingId} className={`${styles.tradeInSection} ${className}`.trim()}>
      <div className={styles.grid}>
        <div className={styles.introColumn}>
          <header className={styles.tradeInIntroduction}>
            <h2 id={headingId} className={`${layout.sectionHeading} ${styles.title}`}>
              {heading}
            </h2>
            <p>{description}</p>
          </header>
          <ul className={styles.highlights}>
            {TRADE_IN_HIGHLIGHTS.map((item) => (
              <li key={item.title}>
                <span className={styles.highlightIcon} aria-hidden="true">
                  <Icon name={item.icon} />
                </span>
                <div>
                  <strong>{item.title}</strong>
                  <span>{item.note}</span>
                </div>
              </li>
            ))}
          </ul>
          <Link
            href="/buying-guide#trade-in"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="How trade-in works (opens in a new tab)"
            className={`${styles.tradeInGuideLink} ${styles.guideAnchor}`}
          >
            How trade-in works <Icon name="next" />
          </Link>
        </div>
        <div className={styles.tradeInCard}>
          <form
            className={styles.tradeInForm}
            onSubmit={(event) => {
              event.preventDefault();
              void tradeIn.valueVehicle();
            }}
          >
            <div className={styles.modeStage} data-failed={optionsUnavailable || undefined}>
              <fieldset
                className={styles.editor}
                data-active={tradeIn.editing}
                disabled={!tradeIn.editing || tradeIn.busy}
                aria-hidden={!tradeIn.editing}
              >
                {tradeIn.taxonomy && (
                  <>
                    <div className={`${styles.fields} ${layout.calmFields}`}>
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
                {optionsUnavailable && (
                  <p className={styles.optionsError} role="status">
                    {unavailableNotice}
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
                      <dt className={styles.allowanceLabel}>{allowanceLabel}</dt>
                      <dd className={styles.allowanceValue}>
                        {formatCurrency(accepted.quote.minValuation)}
                      </dd>
                    </>
                  )}
                </dl>
                <div className={styles.formAction}>
                  {primaryResultAction ?? defaultResultAction}
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
              <div className={styles.retryAction}>
                <Button type="button" variant="secondary" onClick={tradeIn.retryOptions}>
                  Retry vehicle options
                </Button>
              </div>
            )}
          </form>
        </div>
      </div>
    </section>
  );
}
