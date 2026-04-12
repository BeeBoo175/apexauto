"use client";

import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Input } from "@/components/ui/Input";
import { useVehicleFinancing } from "@/hooks/use-vehicle-financing";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import styles from "./VehicleFinancing.module.css";
import layout from "./VehicleLayout.module.css";

export function VehicleFinancing({
  total,
  tradeInAllowance,
}: {
  total: number;
  tradeInAllowance: number;
}) {
  const { cashContribution, setCashContribution, termYears, setTermYears, estimate, error } =
    useVehicleFinancing(total, tradeInAllowance);
  const cash = parseMyr(cashContribution);
  const sliderCash = Math.min(total, cash ?? 0);

  return (
    <section aria-labelledby="vehicle-financing" className={styles.calculator}>
      <header className={styles.sectionHeading}>
        <h2 id="vehicle-financing">Financing estimate</h2>
        <p>Adjust your cash contribution and term.</p>
        <div className={styles.purchaseTotal}>
          <span>Estimated purchase total</span>
          <strong>{formatCurrency(total)}</strong>
        </div>
      </header>
      <div className={layout.columns}>
        <div className={styles.inputs}>
          <div className={styles.fields}>
            <div className={styles.sliderField}>
              <Input
                label="Cash contribution (MYR)"
                id="financing-cash"
                inputMode="decimal"
                maxLength={20}
                value={cashContribution}
                aria-invalid={error ? true : undefined}
                aria-describedby="financing-cash-help financing-error"
                onChange={(event) => setCashContribution(event.target.value)}
              />
              <input
                id="financing-cash-slider"
                aria-label="Adjust cash contribution"
                type="range"
                min="0"
                max={total}
                step="0.01"
                value={sliderCash}
                aria-valuetext={formatCurrency(sliderCash)}
                onChange={(event) => setCashContribution(Number(event.target.value).toFixed(2))}
              />
              <div className={styles.rangeLabels}>
                <span>MYR 0</span>
                <span>{formatCurrency(total)}</span>
              </div>
              <p id="financing-cash-help" className={styles.help}>
                Include any paid MYR 500 deposit in your contribution.
              </p>
            </div>
            <div className={styles.sliderField}>
              <div className={styles.sliderHeading}>
                <label htmlFor="financing-term">Financing term</label>
                <output htmlFor="financing-term">
                  {termYears} {termYears === "1" ? "year" : "years"}
                </output>
              </div>
              <input
                id="financing-term"
                type="range"
                min="1"
                max="9"
                step="1"
                value={termYears}
                aria-valuetext={`${termYears} years`}
                onChange={(event) => setTermYears(event.target.value)}
              />
              <div className={styles.rangeLabels}>
                <span>1 year</span>
                <span>9 years</span>
              </div>
            </div>
          </div>
          <p id="financing-error" role="status" className={styles.error}>
            {error}
          </p>
        </div>
        <div className={styles.result}>
          <div role="status" aria-atomic="true">
            {!error && !estimate && <p>Enter a smaller amount to see an estimate.</p>}
            {error && <p>Update your contribution to see an estimate.</p>}
            {estimate && !error && (
              <>
                <p className={styles.resultLabel}>
                  {estimate.principal === 0
                    ? "No financing required"
                    : "Estimated monthly installment"}
                </p>
                <p className={styles.monthly}>{formatCurrency(estimate.monthlyInstallment)}</p>
                <p className={styles.resultNote}>{termYears}-year term · 3% annual flat interest</p>
                <dl className={styles.summary}>
                  {tradeInAllowance > 0 && (
                    <div className={styles.allowance}>
                      <dt>Trade-in allowance</dt>
                      <dd>{formatCurrency(tradeInAllowance)}</dd>
                    </div>
                  )}
                  <div>
                    <dt>Amount financed</dt>
                    <dd>{formatCurrency(estimate.principal)}</dd>
                  </div>
                  <div>
                    <dt>Total repayments</dt>
                    <dd>{formatCurrency(estimate.totalRepayments)}</dd>
                  </div>
                </dl>
              </>
            )}
          </div>
          {estimate && !error && (
            <details className={styles.breakdown}>
              <summary>
                Repayment breakdown <Icon name="down" />
              </summary>
              <dl className={styles.summary}>
                <div>
                  <dt>Flat interest</dt>
                  <dd>{formatCurrency(estimate.interest)}</dd>
                </div>
                <div>
                  <dt>First {estimate.months - 1} installments</dt>
                  <dd>{formatCurrency(estimate.monthlyInstallment)} each</dd>
                </div>
                <div>
                  <dt>Final installment</dt>
                  <dd>{formatCurrency(estimate.finalInstallment)}</dd>
                </div>
              </dl>
            </details>
          )}
          <p className={styles.qualification}>
            Indicative only. Final purchase terms require confirmation.
          </p>
          <Link
            href="/buying-guide#financing"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="How this estimate works (opens in a new tab)"
            className={styles.guideLink}
          >
            How this estimate works <Icon name="next" />
          </Link>
        </div>
      </div>
    </section>
  );
}
