"use client";

import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import { formatCurrency } from "@/lib/currency-format";
import { readSaleTerms } from "@/lib/sales/sale-terms";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/staff/StaffForms.module.css";

// The terms the handling agent controls: negotiated discount, one eligible warranty and one of the customer's
// trade-ins. The editor keeps the draft from its base version until it is saved.
export function SaleTermsForm({
  editor,
  disabled,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  disabled: boolean;
}) {
  const base = editor.base.sale;
  const { terms, options, changeTerms } = editor;
  const { negotiated, credit, chosen, overValuation, valid } = readSaleTerms(terms, options);

  function submit() {
    if (!valid || negotiated === null || credit === null) {
      return;
    }
    void editor.run("term-revisions", {
      warrantyPackageId: terms.warranty || null,
      negotiatedDiscount: negotiated,
      tradeInAppointmentId: terms.tradeIn || null,
      tradeInAllowance: terms.tradeIn ? credit : 0,
    });
  }

  return (
    <form
      className={workbench.stack}
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
    >
      <div className={workbench.sectionHeader}>
        <h2 className={workbench.sectionTitle}>Agreed terms</h2>
      </div>
      {base.status === "Approved" && (
        <p className={workbench.help}>
          Saving changes withdraws the approval; the sale must be submitted again.
        </p>
      )}
      <Input
        label="Negotiated discount (MYR)"
        density="compact"
        inputMode="decimal"
        value={terms.discount}
        disabled={disabled}
        onChange={(event) => changeTerms({ discount: event.target.value })}
      />
      <p className={negotiated === null ? forms.error : forms.hint}>
        {negotiated === null
          ? "Enter an amount with up to two decimals."
          : "Above the agent limit, the sale needs a manager's approval."}
      </p>
      <Select
        label="Warranty package"
        density="compact"
        value={terms.warranty}
        disabled={disabled || !options}
        onChange={(event) => changeTerms({ warranty: event.target.value })}
      >
        <option value="">No warranty</option>
        {options?.warranties.map((item) => (
          <option key={item.id} value={item.id}>
            {item.name} · {formatCurrency(item.price)}
          </option>
        ))}
        {terms.warranty && !options?.warranties.some((item) => item.id === terms.warranty) && (
          <option value={terms.warranty}>{base.warrantyName ?? "Current warranty"}</option>
        )}
      </Select>
      <Select
        label="Trade-in vehicle"
        density="compact"
        value={terms.tradeIn}
        disabled={disabled || !options}
        onChange={(event) => {
          const tradeIn = event.target.value;
          changeTerms(tradeIn ? { tradeIn } : { tradeIn, allowance: "" });
        }}
      >
        <option value="">No trade-in</option>
        {options?.tradeIns.map((item) => (
          <option key={item.appointmentId} value={item.appointmentId}>
            {item.label}
          </option>
        ))}
        {terms.tradeIn &&
          !options?.tradeIns.some((item) => item.appointmentId === terms.tradeIn) && (
            <option value={terms.tradeIn}>{base.tradeInLabel ?? "Current trade-in"}</option>
          )}
      </Select>
      {terms.tradeIn && (
        <>
          <Input
            label="Trade-in allowance (MYR)"
            density="compact"
            inputMode="decimal"
            value={terms.allowance}
            disabled={disabled}
            onChange={(event) => changeTerms({ allowance: event.target.value })}
          />
          <p className={credit === null || overValuation ? forms.error : forms.hint}>
            {chosen
              ? `Valuation ${formatCurrency(chosen.minimum)}–${formatCurrency(chosen.maximum)}; the allowance cannot exceed the maximum.`
              : "Enter the agreed allowance."}
          </p>
        </>
      )}
      {editor.optionsFailed && (
        <p className={forms.error}>
          Warranty and trade-in choices could not be loaded. Reopen to retry.
        </p>
      )}
      <div>
        <Button
          type="submit"
          size="compact"
          disabled={disabled || !valid || (!editor.dirty && editor.uncertain !== "term-revisions")}
        >
          {editor.uncertain === "term-revisions" ? "Reconcile saved terms" : "Save terms"}
        </Button>
      </div>
    </form>
  );
}
