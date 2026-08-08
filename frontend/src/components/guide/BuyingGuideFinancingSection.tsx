"use client";

import { useState } from "react";
import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import layout from "@/components/vehicles/VehicleLayout.module.css";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { DEFAULT_HOLD_DEPOSIT, percentageOfMyr } from "@/lib/guide/buying-guide-math";
import { calculateFinancing, defaultCashContribution } from "@/lib/vehicles/financing";
import guideLayout from "./GuideLayout.module.css";
import styles from "./BuyingGuideSections.module.css";

const termYearsOptions = [1, 2, 3, 4, 5, 6, 7, 8, 9] as const;
const downPaymentRatios = [10, 20, 30] as const;
const tradeInOptions = [
  { id: "none", label: "No trade-in (RM 0)", amount: 0 },
  { id: "small", label: "RM 12,000 trade-in", amount: 12000 },
  { id: "mid", label: "RM 25,000 trade-in", amount: 25000 },
] as const;

export function BuyingGuideFinancingSection() {
  const [totalDraft, setTotalDraft] = useState("60000.00");
  const [cashDraft, setCashDraft] = useState(() => defaultCashContribution(60000));
  const [termYears, setTermYears] = useState<number>(7);
  const [tradeInId, setTradeInId] = useState<string>("custom");
  const [customTradeIn, setCustomTradeIn] = useState("9000.00");
  const [holdIncluded, setHoldIncluded] = useState(true);

  const parsedTotal = parseMyr(totalDraft);
  const parsedCash = parseMyr(cashDraft);
  const parsedTradeIn = parseMyr(customTradeIn);

  const estimate =
    parsedTotal !== null &&
    parsedTotal > 0 &&
    parsedCash !== null &&
    parsedTradeIn !== null &&
    parsedTradeIn >= 0
      ? calculateFinancing(parsedTotal, parsedCash, termYears, parsedTradeIn)
      : null;

  const holdCoversOrExceedsCash =
    holdIncluded && parsedCash !== null && parsedCash < DEFAULT_HOLD_DEPOSIT;
  const additionalCashDue =
    parsedCash !== null && holdIncluded
      ? Math.max(0, Number((parsedCash - DEFAULT_HOLD_DEPOSIT).toFixed(2)))
      : parsedCash;

  function applyDownPaymentPercent(percent: number) {
    if (parsedTotal === null || parsedTotal <= 0) {
      return;
    }
    const nextCash = percentageOfMyr(parsedTotal, percent);
    if (nextCash !== null) {
      setCashDraft(nextCash);
    }
  }

  function applyTradeInPreset(option: (typeof tradeInOptions)[number]) {
    setTradeInId(option.id);
    setCustomTradeIn(option.amount.toFixed(2));
  }

  const financedPrincipal = estimate?.principal ?? 0;
  const totalInterest = estimate?.interest ?? 0;
  const appliedCash = parsedCash ?? 0;
  const appliedTradeIn = parsedTradeIn ?? 0;
  const compositionSum = Math.max(
    1,
    appliedCash + appliedTradeIn + financedPrincipal + totalInterest,
  );
  const cashPct = (appliedCash / compositionSum) * 100;
  const tradeInPct = (appliedTradeIn / compositionSum) * 100;
  const principalPct = (financedPrincipal / compositionSum) * 100;
  const interestPct = (totalInterest / compositionSum) * 100;

  return (
    <div className={styles.split}>
      <div className={`${styles.formColumn} ${layout.calmFields}`}>
        <div className={styles.fieldGrid}>
          <Input
            id="guide-fin-total"
            label="Car / OTR budget (MYR)"
            inputMode="decimal"
            maxLength={14}
            value={totalDraft}
            aria-invalid={parsedTotal === null || parsedTotal <= 0 ? true : undefined}
            onChange={(event) => setTotalDraft(event.target.value)}
          />
          <Select
            id="guide-fin-term"
            label="Loan tenure"
            value={String(termYears)}
            onChange={(event) => setTermYears(Number(event.target.value))}
          >
            {termYearsOptions.map((years) => (
              <option key={years} value={years}>
                {years} {years === 1 ? "year" : "years"} ({years * 12} months)
              </option>
            ))}
          </Select>
        </div>

        <div className={styles.chipGroup}>
          <span className={styles.chipGroupLabel}>Down payment presets</span>
          <div className={styles.presetGrid}>
            {downPaymentRatios.map((percent) => {
              const target =
                parsedTotal !== null && parsedTotal > 0
                  ? percentageOfMyr(parsedTotal, percent)
                  : null;
              const isSelected = target !== null && parsedCash === Number(target);
              return (
                <button
                  key={percent}
                  type="button"
                  className={guideLayout.chip}
                  aria-pressed={isSelected}
                  onClick={() => applyDownPaymentPercent(percent)}
                >
                  {percent}% cash down
                </button>
              );
            })}
          </div>
          <Input
            id="guide-fin-cash"
            label="Cash down payment (MYR)"
            inputMode="decimal"
            maxLength={14}
            value={cashDraft}
            aria-invalid={parsedCash === null ? true : undefined}
            onChange={(event) => setCashDraft(event.target.value)}
          />
        </div>

        <div className={styles.chipGroup}>
          <span className={styles.chipGroupLabel}>Trade-in allowance</span>
          <div className={styles.presetGrid}>
            {tradeInOptions.map((option) => (
              <button
                key={option.id}
                type="button"
                className={guideLayout.chip}
                aria-pressed={tradeInId === option.id && parsedTradeIn === option.amount}
                onClick={() => applyTradeInPreset(option)}
              >
                {option.label}
              </button>
            ))}
          </div>
          <Input
            id="guide-fin-trade-in"
            label="Estimated trade-in credit (MYR)"
            inputMode="decimal"
            maxLength={14}
            value={customTradeIn}
            aria-invalid={parsedTradeIn === null ? true : undefined}
            onChange={(event) => {
              setTradeInId("custom");
              setCustomTradeIn(event.target.value);
            }}
          />
        </div>

        <div className={styles.toggleBar}>
          <div className={styles.toggleCopy}>
            <strong>RM 500 hold deposit already paid</strong>
            <span>Counted inside your down payment so it is never charged twice.</span>
          </div>
          <button
            type="button"
            className={guideLayout.chip}
            aria-pressed={holdIncluded}
            onClick={() => setHoldIncluded((prev) => !prev)}
          >
            {holdIncluded ? "Hold credit active" : "Hold not paid yet"}
          </button>
        </div>
      </div>

      <aside className={guideLayout.receiptPanel} aria-label="Estimated monthly repayment schedule">
        <div className={guideLayout.receiptHeader}>
          <h3 className={layout.cardHeading}>Estimated monthly plan</h3>
          <span className={guideLayout.receiptMeta}>3% p.a. flat</span>
        </div>

        {estimate === null ? (
          <p className={styles.fieldError}>
            Enter positive MYR amounts (up to two decimals) to preview monthly payments.
          </p>
        ) : (
          <>
            <div className={styles.installmentHero} aria-live="polite" aria-atomic="true">
              {estimate.principal === 0 ? (
                <>
                  <span>Financed balance</span>
                  <strong>No financing required</strong>
                </>
              ) : (
                <>
                  <span>First {estimate.months - 1} months</span>
                  <strong>{formatCurrency(estimate.monthlyInstallment)} / mo</strong>
                </>
              )}
            </div>

            <div className={styles.proportionWrap}>
              <p className="sr-only">
                Repayment mix: {Math.round(cashPct)}% cash, {Math.round(tradeInPct)}% trade-in,{" "}
                {Math.round(principalPct)}% financed principal, and {Math.round(interestPct)}% flat
                interest.
              </p>
              <div className={styles.proportionBar} aria-hidden="true">
                {cashPct > 0 && (
                  <span className={styles.segCash} style={{ width: `${cashPct}%` }} />
                )}
                {tradeInPct > 0 && (
                  <span className={styles.segTradeIn} style={{ width: `${tradeInPct}%` }} />
                )}
                {principalPct > 0 && (
                  <span className={styles.segPrincipal} style={{ width: `${principalPct}%` }} />
                )}
                {interestPct > 0 && (
                  <span className={styles.segInterest} style={{ width: `${interestPct}%` }} />
                )}
              </div>
              <div className={styles.proportionLegend} aria-hidden="true">
                <span className={styles.legendItem}>
                  <span className={`${styles.legendSwatch} ${styles.segCash}`} /> Cash
                </span>
                {appliedTradeIn > 0 && (
                  <span className={styles.legendItem}>
                    <span className={`${styles.legendSwatch} ${styles.segTradeIn}`} /> Trade-in
                  </span>
                )}
                <span className={styles.legendItem}>
                  <span className={`${styles.legendSwatch} ${styles.segPrincipal}`} /> Principal
                </span>
                <span className={styles.legendItem}>
                  <span className={`${styles.legendSwatch} ${styles.segInterest}`} /> 3% interest
                </span>
              </div>
            </div>

            <dl className={guideLayout.ledger}>
              <div className={guideLayout.ledgerRow}>
                <dt>
                  Cash down payment
                  {holdIncluded && !holdCoversOrExceedsCash && additionalCashDue !== null && (
                    <span className={guideLayout.ledgerSubline}>
                      Includes RM 500 hold · {formatCurrency(additionalCashDue)} due later
                    </span>
                  )}
                </dt>
                <dd>{formatCurrency(parsedCash ?? 0)}</dd>
              </div>
              {(parsedTradeIn ?? 0) > 0 && (
                <div className={`${guideLayout.ledgerRow} ${guideLayout.ledgerDiscount}`}>
                  <dt>Trade-in credit</dt>
                  <dd>−{formatCurrency(parsedTradeIn ?? 0)}</dd>
                </div>
              )}
              <div className={guideLayout.ledgerRow}>
                <dt>Financed principal</dt>
                <dd>{formatCurrency(estimate.principal)}</dd>
              </div>
              <div className={guideLayout.ledgerRow}>
                <dt>
                  Total interest ({termYears} {termYears === 1 ? "yr" : "yrs"} × 3%)
                </dt>
                <dd>{formatCurrency(estimate.interest)}</dd>
              </div>
            </dl>

            <div className={styles.totalBand}>
              <span>
                Total repayments
                {estimate.principal > 0 && (
                  <span className={guideLayout.ledgerSubline}>
                    Final month ({estimate.months}): {formatCurrency(estimate.finalInstallment)}
                  </span>
                )}
              </span>
              <strong>{formatCurrency(estimate.totalRepayments)}</strong>
            </div>

            {holdCoversOrExceedsCash && (
              <p className={guideLayout.receiptNote}>
                Entered cash is below the RM 500 hold deposit; adjust cash down or turn off hold
                credit.
              </p>
            )}

            <p className={guideLayout.receiptNote}>
              Indicative branch estimate rounded to nearest sen.
            </p>
          </>
        )}

        <div>
          <Link href="/disclaimer" prefetch={false} className={buttonClassName("secondary", "md")}>
            View financing notes
          </Link>
        </div>
      </aside>
    </div>
  );
}
