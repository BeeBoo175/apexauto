"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import type { useSettingsSave } from "@/hooks/use-settings-save";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import { parseMyr } from "@/lib/currency-input";
import { dealPath, parseDealOptions, type Deal, type DealOptions } from "@/lib/deals/deals";
import { SettingsNotice } from "@/components/settings/SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/settings/SettingsForms.module.css";

// Edits the agent controls: negotiated discount, one eligible warranty and one of the customer's trade-ins.
export function DealTermsForm({
  base,
  saver,
  disabled,
  onSaved,
  onReload,
  onDirty,
}: {
  base: Deal;
  saver: ReturnType<typeof useSettingsSave<Deal>>;
  disabled: boolean;
  onSaved: () => void;
  onReload: () => void;
  onDirty: (dirty: boolean) => void;
}) {
  const toast = useToast();
  const [options, setOptions] = useState<DealOptions | null>(null);
  const [failed, setFailed] = useState(false);
  const [discount, setDiscount] = useState(base.negotiatedDiscount.toFixed(2));
  const [warranty, setWarranty] = useState(base.warrantyPackageId ?? "");
  const [tradeIn, setTradeIn] = useState(base.tradeInAppointmentId ?? "");
  const [allowance, setAllowance] = useState(
    base.tradeInAllowance > 0 ? base.tradeInAllowance.toFixed(2) : "",
  );

  useEffect(() => {
    const attempt = new AbortController();
    privateApiFetch(`${dealPath(base.id)}/options`, parseDealOptions, { signal: attempt.signal })
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
  }, [base.id]);

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
  useEffect(() => onDirty(changed), [changed, onDirty]);

  async function submit() {
    if (!valid || negotiated === null || credit === null) {
      return;
    }
    const saved = await saver.save(dealPath(base.id), "PUT", {
      expectedVersion: base.version,
      warrantyPackageId: warranty || null,
      negotiatedDiscount: negotiated,
      tradeInAppointmentId: tradeIn || null,
      tradeInAllowance: tradeIn ? credit : 0,
    });
    if (saved) {
      toast.success("Deal terms saved.");
      onSaved();
    }
  }

  return (
    <form
      className={workbench.stack}
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <h3>Edit terms</h3>
      {base.status === "Approved" && (
        <p className={workbench.help}>
          Saving changes sends this approved deal back to draft; it must be submitted again.
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
          : "Above the agent limit, the deal needs a manager's approval."}
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
          <option value={tradeIn}>Current trade-in</option>
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
      {saver.notice && (
        <SettingsNotice
          notice={saver.notice}
          busy={disabled}
          conflictMessage="This deal changed elsewhere. Reload it to compare; your entries are kept."
          reloadLabel="Reload deal"
          onReload={() => {
            saver.clearNotice();
            onReload();
          }}
        />
      )}
      <div>
        <Button type="submit" size="compact" disabled={disabled || !valid || !changed}>
          Save terms
        </Button>
      </div>
    </form>
  );
}
