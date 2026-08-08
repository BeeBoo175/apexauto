"use client";

import { useState } from "react";
import Link from "next/link";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import { Input, Select } from "@/components/ui/Input";
import { useVehicleFinancing } from "@/hooks/use-vehicle-financing";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import styles from "./VehicleFinancing.module.css";
import layout from "./VehicleLayout.module.css";

export interface FinancingPreset {
  id: string;
  label: string;
  total: number;
}

interface VehicleFinancingProps {
  total: number;
  tradeInAllowance?: number;
  headingId?: string;
  heading?: string;
  description?: string;
  presets?: readonly FinancingPreset[];
  allowCustomTotal?: boolean;
  browseHref?: (targetTotal: number) => string;
  onClearTradeIn?: () => void;
}

export function VehicleFinancing({
  total,
  tradeInAllowance = 0,
  headingId = "vehicle-financing",
  heading = "Financing estimate",
  description,
  presets,
  allowCustomTotal = false,
  browseHref,
  onClearTradeIn,
}: VehicleFinancingProps) {
  const [selectedPresetId, setSelectedPresetId] = useState(presets?.[0]?.id ?? "custom");
  const [customTotalDraft, setCustomTotalDraft] = useState(() => total.toFixed(2));
  const parsedCustom = parseMyr(customTotalDraft);
  const customTotal =
    allowCustomTotal && parsedCustom !== null && parsedCustom > 0 ? parsedCustom : null;
  const effectiveTotal = customTotal ?? total;
  const totalError =
    allowCustomTotal && customTotal === null ? "Enter a positive vehicle price in MYR." : null;

  const { cashContribution, setCashContribution, termYears, setTermYears, estimate, error } =
    useVehicleFinancing(effectiveTotal, tradeInAllowance);
  const cash = parseMyr(cashContribution);
  const sliderCash = Math.min(effectiveTotal, cash ?? 0);
  const appliedCash = Math.max(0, cash ?? 0);
  const mixSum =
    estimate && !error
      ? Math.max(1, appliedCash + tradeInAllowance + estimate.principal + estimate.interest)
      : 1;
  const cashMixPct = (appliedCash / mixSum) * 100;
  const tradeInMixPct = (tradeInAllowance / mixSum) * 100;
  const principalMixPct = ((estimate?.principal ?? 0) / mixSum) * 100;
  const interestMixPct = ((estimate?.interest ?? 0) / mixSum) * 100;

  function selectPreset(id: string) {
    setSelectedPresetId(id);
    const match = presets?.find((item) => item.id === id);
    if (match) {
      setCustomTotalDraft(match.total.toFixed(2));
    }
  }

  return (
    <section aria-labelledby={headingId} className={styles.calculator}>
      <div className={layout.columns}>
        <div className={styles.inputs}>
          <header className={styles.sectionHeading}>
            <h2 id={headingId} className={`${layout.sectionHeading} ${styles.title}`}>
              {heading}
            </h2>
            {description && <p className={styles.sectionDescription}>{description}</p>}
          </header>
          <div className={`${styles.fields} ${layout.calmFields}`}>
            {allowCustomTotal && (
              <div className={styles.budgetGroup}>
                {presets && presets.length > 0 && (
                  <Select
                    label="Sample showroom vehicle or preset"
                    id={`${headingId}-preset`}
                    value={selectedPresetId}
                    onChange={(event) => selectPreset(event.target.value)}
                  >
                    {presets.map((preset) => (
                      <option key={preset.id} value={preset.id}>
                        {preset.label} ({formatCurrency(preset.total)})
                      </option>
                    ))}
                    <option value="custom">Custom vehicle price</option>
                  </Select>
                )}
                <Input
                  label="Target vehicle price (MYR)"
                  id={`${headingId}-total`}
                  inputMode="decimal"
                  maxLength={20}
                  value={customTotalDraft}
                  aria-invalid={totalError ? true : undefined}
                  onChange={(event) => {
                    setSelectedPresetId("custom");
                    setCustomTotalDraft(event.target.value);
                  }}
                />
              </div>
            )}
            <div className={styles.sliderField}>
              <Input
                label="Cash contribution (MYR)"
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
                max={effectiveTotal}
                step="0.01"
                value={sliderCash}
                aria-valuetext={formatCurrency(sliderCash)}
                onChange={(event) => setCashContribution(Number(event.target.value).toFixed(2))}
              />
              <div className={styles.rangeLabels}>
                <span>MYR 0</span>
                <span>{formatCurrency(effectiveTotal)}</span>
              </div>
              <p id={`${headingId}-cash-help`} className={styles.help}>
                Includes any RM 500 hold deposit paid online.
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
            {totalError ?? error}
          </p>
        </div>
        <div className={styles.result}>
          <div role="status" aria-atomic="true" className={styles.resultStatus}>
            {(totalError || (!error && !estimate)) && (
              <p>Enter a valid target price and contribution to see an estimate.</p>
            )}
            {!totalError && error && <p>Update your contribution to see an estimate.</p>}
            {!totalError && estimate && !error && (
              <>
                <p className={styles.resultLabel}>
                  {estimate.principal === 0
                    ? "No financing required"
                    : "Estimated monthly installment"}
                </p>
                <p className={styles.monthly}>{formatCurrency(estimate.monthlyInstallment)}</p>
                <p className={styles.resultNote}>{termYears}-year term · 3% annual flat interest</p>
                <div className={styles.mixWrap}>
                  <div className={styles.mixBar} aria-hidden="true">
                    {cashMixPct > 0 && (
                      <span className={styles.mixCash} style={{ width: `${cashMixPct}%` }} />
                    )}
                    {tradeInMixPct > 0 && (
                      <span className={styles.mixTradeIn} style={{ width: `${tradeInMixPct}%` }} />
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
                    {tradeInAllowance > 0 && (
                      <span>
                        <i className={styles.mixTradeIn} /> Trade-in
                      </span>
                    )}
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
                    <dt>
                      {allowCustomTotal ? "Target vehicle budget" : "Estimated purchase total"}
                    </dt>
                    <dd>{formatCurrency(effectiveTotal)}</dd>
                  </div>
                  {tradeInAllowance > 0 && (
                    <div className={styles.allowance}>
                      <dt>
                        <span className={styles.allowanceTitle}>
                          <span>Trade-in allowance</span>
                          {onClearTradeIn && (
                            <Button
                              type="button"
                              variant="ghost"
                              size="row"
                              onClick={onClearTradeIn}
                            >
                              Remove
                            </Button>
                          )}
                        </span>
                      </dt>
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
          {!totalError && estimate && !error && (
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
          {browseHref && !totalError && (
            <div className={styles.browseAction}>
              <Link
                href={browseHref(effectiveTotal)}
                prefetch={false}
                className={buttonClassName("primary", "md")}
              >
                Browse cars under {formatCurrency(effectiveTotal)}
              </Link>
            </div>
          )}
          <p className={styles.qualification}>
            Indicative only. Final purchase terms require confirmation.
          </p>
          <Link
            href="/buying-guide#financing"
            target="_blank"
            rel="noopener noreferrer"
            aria-label="How this estimate works (opens in a new tab)"
            className={layout.guideLink}
          >
            How this estimate works <Icon name="next" />
          </Link>
        </div>
      </div>
    </section>
  );
}
