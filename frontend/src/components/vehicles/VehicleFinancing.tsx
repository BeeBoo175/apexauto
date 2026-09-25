"use client";

import { RESERVATION_DEPOSIT } from "@/lib/vehicles/deposit-terms";

import Link from "next/link";
import { Icon } from "@/components/ui/Icon";
import { Input } from "@/components/ui/Input";
import { SegmentedControl } from "@/components/ui/SegmentedControl";
import { useCountedNumber } from "@/hooks/use-counted-number";
import { useVehicleFinancing } from "@/hooks/use-vehicle-financing";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { cashShare } from "@/lib/vehicles/financing";
import styles from "./VehicleFinancing.module.css";
import layout from "./VehicleLayout.module.css";

const DEPOSIT_PERCENT_PRESETS = [10, 20, 30] as const;

interface VehicleFinancingProps {
  total: number;
  headingId?: string;
}

export function VehicleFinancing({
  total,
  headingId = "vehicle-financing",
}: VehicleFinancingProps) {
  const { cashContribution, setCashContribution, termYears, setTermYears, estimate, error } =
    useVehicleFinancing(total);
  const monthlyShown = useCountedNumber(estimate?.monthlyInstallment ?? 0);
  const cash = parseMyr(cashContribution);
  const sliderCash = Math.min(total, cash ?? 0);
  const appliedCash = Math.max(0, cash ?? 0);
  const mixSum =
    estimate && !error ? Math.max(1, appliedCash + estimate.principal + estimate.interest) : 1;
  const cashMixPct = (appliedCash / mixSum) * 100;
  const principalMixPct = ((estimate?.principal ?? 0) / mixSum) * 100;
  const interestMixPct = ((estimate?.interest ?? 0) / mixSum) * 100;

  return (
    <section aria-labelledby={headingId} className={styles.calculator}>
      <div className={layout.columns}>
        <div className={styles.inputs}>
          <header className={styles.sectionHeading}>
            <h2 id={headingId} className={`${layout.sectionHeading} ${styles.title}`}>
              Financing estimate
            </h2>
          </header>
          <div className={`${styles.fields} ${layout.calmFields}`}>
            <div className={styles.sliderField}>
              <Input
                label="Cash contribution (RM)"
                id={`${headingId}-cash`}
                inputMode="decimal"
                maxLength={20}
                value={cashContribution}
                aria-invalid={error ? true : undefined}
                aria-describedby={`${headingId}-cash-help ${headingId}-error`}
                onChange={(event) => setCashContribution(event.target.value)}
              />
              <input
                id={`${headingId}-cash-slider`}
                aria-label="Adjust cash contribution"
                type="range"
                min="0"
                max={total}
                step="0.01"
                value={sliderCash}
                aria-valuetext={formatCurrency(sliderCash)}
                onChange={(event) => setCashContribution(Number(event.target.value).toFixed(2))}
              />
              <SegmentedControl
                label="Deposit as a share of the price"
                options={DEPOSIT_PERCENT_PRESETS.map((percent) => {
                  const share = cashShare(total, percent);
                  return {
                    key: String(percent),
                    label: `${percent}%`,
                    selected: cash !== null && cash === parseMyr(share),
                    onSelect: () => setCashContribution(share),
                  };
                })}
              />
              <div className={styles.rangeLabels}>
                <span>RM 0</span>
                <span>{formatCurrency(total)}</span>
              </div>
              <p id={`${headingId}-cash-help`} className={styles.help}>
                Includes any {formatCurrency(RESERVATION_DEPOSIT, "RM")} hold deposit paid online.
              </p>
            </div>
            <div className={styles.sliderField}>
              <div className={styles.sliderHeading}>
                <label htmlFor={`${headingId}-term`}>Financing term</label>
                <output htmlFor={`${headingId}-term`}>
                  {termYears} {termYears === "1" ? "year" : "years"}
                </output>
              </div>
              <input
                id={`${headingId}-term`}
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
          <p id={`${headingId}-error`} role="status" className={styles.error}>
            {error}
          </p>
        </div>
        <div className={styles.result}>
          <div role="status" aria-atomic="true" className={styles.resultStatus}>
            {!error && !estimate && (
              <p>Enter a valid target price and contribution to see an estimate.</p>
            )}
            {error && <p>Update your contribution to see an estimate.</p>}
            {estimate && !error && (
              <>
                <p className={styles.resultLabel}>
                  {estimate.principal === 0
                    ? "No financing required"
                    : "Estimated monthly installment"}
                </p>
                <p className={styles.monthly}>{formatCurrency(monthlyShown)}</p>
                <p className={styles.resultNote}>{termYears}-year term · 3% annual flat interest</p>
                <div className={styles.mixWrap}>
                  <div className={styles.mixBar} aria-hidden="true">
                    {cashMixPct > 0 && (
                      <span className={styles.mixCash} style={{ width: `${cashMixPct}%` }} />
                    )}
                    {principalMixPct > 0 && (
                      <span
                        className={styles.mixPrincipal}
                        style={{ width: `${principalMixPct}%` }}
                      />
                    )}
                    {interestMixPct > 0 && (
                      <span
                        className={styles.mixInterest}
                        style={{ width: `${interestMixPct}%` }}
                      />
                    )}
                  </div>
                  <div className={styles.mixLegend} aria-hidden="true">
                    <span>
                      <i className={styles.mixCash} /> Cash
                    </span>
                    <span>
                      <i className={styles.mixPrincipal} /> Principal
                    </span>
                    <span>
                      <i className={styles.mixInterest} /> Interest
                    </span>
                  </div>
                </div>
                <dl className={styles.summary}>
                  <div>
                    <dt>Estimated purchase total</dt>
                    <dd>{formatCurrency(total)}</dd>
                  </div>
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
            href="/buying-guide#settle"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="How this estimate works (opens in a new tab)"
            className={`${layout.guideLink} ${styles.resultLink}`}
          >
            How this estimate works <Icon name="next" />
          </Link>
        </div>
      </div>
    </section>
  );
}
