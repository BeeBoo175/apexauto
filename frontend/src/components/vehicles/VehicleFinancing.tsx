"use client";

import { Input, Select } from "@/components/ui/Input";
import { useVehicleFinancing } from "@/hooks/use-vehicle-financing";
import { formatCurrency } from "@/lib/currency-format";
import styles from "./VehicleFinancing.module.css";

export function VehicleFinancing({ total }: { total: number }) {
  const { cashContribution, setCashContribution, termYears, setTermYears, estimate, error } =
    useVehicleFinancing(total);

  return (
    <section aria-labelledby="vehicle-financing" className={styles.calculator}>
      <h2 id="vehicle-financing">Financing estimate</h2>
      <p>
        Local portfolio estimate at 3% annual flat interest, before any trade-in allowance. This is
        not a financing offer. Final purchase terms are subject to confirmation.
      </p>
      <div className={styles.fields}>
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
        <Select
          label="Financing term"
          id="financing-term"
          value={termYears}
          onChange={(event) => setTermYears(event.target.value)}
        >
          {Array.from({ length: 9 }, (_, index) => index + 1).map((years) => (
            <option key={years} value={years}>
              {years} {years === 1 ? "year" : "years"}
            </option>
          ))}
        </Select>
      </div>
      <p id="financing-cash-help">
        Defaults to 10% of the displayed total. Include any paid MYR 500 reservation deposit in your
        cash contribution; it is not deducted a second time.
      </p>
      <p id="financing-error" role="status">
        {error}
      </p>
      {!error && !estimate && (
        <p>These amounts cannot be estimated safely. Enter a smaller amount.</p>
      )}
      {estimate && (
        <div role="status" aria-atomic="true">
          {estimate.principal === 0 && <p>No financing required</p>}
          <dl className={styles.summary}>
            <div>
              <dt>Estimated principal</dt>
              <dd>{formatCurrency(estimate.principal)}</dd>
            </div>
            <div>
              <dt>Flat interest (3% annually)</dt>
              <dd>{formatCurrency(estimate.interest)}</dd>
            </div>
            <div>
              <dt>Total financed repayments</dt>
              <dd>{formatCurrency(estimate.totalRepayments)}</dd>
            </div>
            <div>
              <dt>First {estimate.months - 1} monthly installments</dt>
              <dd>{formatCurrency(estimate.monthlyInstallment)} each</dd>
            </div>
            <div>
              <dt>Final installment</dt>
              <dd>{formatCurrency(estimate.finalInstallment)}</dd>
            </div>
          </dl>
        </div>
      )}
    </section>
  );
}
