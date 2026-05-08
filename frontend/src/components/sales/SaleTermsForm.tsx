"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import type { useSaleEditor } from "@/hooks/use-sale-editor";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { parseSaleOptions, salePath, type SaleOptions } from "@/lib/sales/sale";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/staff/StaffForms.module.css";

// The terms the handling agent controls: negotiated discount, one eligible warranty and one of the customer's
// trade-ins. The form starts from the editor's base version and keeps its entries until they are saved.
export function SaleTermsForm({
  editor,
  disabled,
}: {
  editor: ReturnType<typeof useSaleEditor>;
  disabled: boolean;
}) {
  const base = editor.base.sale;
  const id = editor.base.id;
  const [options, setOptions] = useState<SaleOptions | null>(null);
  const [failed, setFailed] = useState(false);
  const [discount, setDiscount] = useState(base.negotiatedDiscount.toFixed(2));
  const [warranty, setWarranty] = useState(base.warrantyPackageId ?? "");
  const [tradeIn, setTradeIn] = useState(base.tradeInAppointmentId ?? "");
  const [allowance, setAllowance] = useState(
    base.tradeInAllowance > 0 ? base.tradeInAllowance.toFixed(2) : "",
  );

  useEffect(() => {
    const attempt = new AbortController();
    privateApiFetch(`${salePath(id)}/options`, parseSaleOptions, { signal: attempt.signal })
      .then((result) => {
        if (result.status === 200 && result.data) {
          setOptions(result.data);
        } else {
          setFailed(true);
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setFailed(true);
        }
      });
    return () => attempt.abort();
  }, [id]);

  const negotiated = parseMyr(discount.trim());
  const credit = tradeIn ? parseMyr(allowance.trim()) : 0;
  const chosen = options?.tradeIns.find((item) => item.appointmentId === tradeIn);
  const valid =
    negotiated !== null &&
    credit !== null &&
    (!tradeIn || (credit > 0 && (!chosen || credit <= chosen.maximum)));
  const changed =
    negotiated !== base.negotiatedDiscount ||
    (warranty || null) !== base.warrantyPackageId ||
    (tradeIn || null) !== base.tradeInAppointmentId ||
    (credit ?? 0) !== base.tradeInAllowance;
  const { markDirty } = editor;
  useEffect(() => markDirty(changed), [changed, markDirty]);

  function submit() {
    if (!valid || negotiated === null || credit === null) {
      return;
    }
    void editor.run("terms", {
      warrantyPackageId: warranty || null,
      negotiatedDiscount: negotiated,
      tradeInAppointmentId: tradeIn || null,
      tradeInAllowance: tradeIn ? credit : 0,
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
        value={discount}
        disabled={disabled}
        onChange={(event) => setDiscount(event.target.value)}
      />
      <p className={negotiated === null ? forms.error : forms.hint}>
        {negotiated === null
          ? "Enter an amount with up to two decimals."
          : "Above the agent limit, the sale needs a manager's approval."}
      </p>
      <Select
        label="Warranty package"
        density="compact"
        value={warranty}
        disabled={disabled || !options}
        onChange={(event) => setWarranty(event.target.value)}
      >
        <option value="">No warranty</option>
        {options?.warranties.map((item) => (
          <option key={item.id} value={item.id}>
            {item.name} · {formatCurrency(item.price)}
          </option>
        ))}
        {warranty && !options?.warranties.some((item) => item.id === warranty) && (
          <option value={warranty}>{base.warrantyName ?? "Current warranty"}</option>
        )}
      </Select>
      <Select
        label="Trade-in vehicle"
        density="compact"
        value={tradeIn}
        disabled={disabled || !options}
        onChange={(event) => {
          setTradeIn(event.target.value);
          if (!event.target.value) {
            setAllowance("");
          }
        }}
      >
        <option value="">No trade-in</option>
        {options?.tradeIns.map((item) => (
          <option key={item.appointmentId} value={item.appointmentId}>
            {item.label}
          </option>
        ))}
        {tradeIn && !options?.tradeIns.some((item) => item.appointmentId === tradeIn) && (
          <option value={tradeIn}>{base.tradeInLabel ?? "Current trade-in"}</option>
        )}
      </Select>
      {tradeIn && (
        <>
          <Input
            label="Trade-in allowance (MYR)"
            density="compact"
            inputMode="decimal"
            value={allowance}
            disabled={disabled}
            onChange={(event) => setAllowance(event.target.value)}
          />
          <p
            className={
              credit === null || (chosen && (credit ?? 0) > chosen.maximum)
                ? forms.error
                : forms.hint
            }
          >
            {chosen
              ? `Valuation ${formatCurrency(chosen.minimum)}–${formatCurrency(chosen.maximum)}; the allowance cannot exceed the maximum.`
              : "Enter the agreed allowance."}
          </p>
        </>
      )}
      {failed && (
        <p className={forms.error}>
          Warranty and trade-in choices could not be loaded. Reopen to retry.
        </p>
      )}
      <div>
        <Button
          type="submit"
          size="compact"
          disabled={disabled || !valid || (!changed && editor.uncertain !== "terms")}
        >
          {editor.uncertain === "terms" ? "Reconcile saved terms" : "Save terms"}
        </Button>
      </div>
    </form>
  );
}
