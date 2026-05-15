"use client";

import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { usePromoForm } from "@/hooks/use-promo-form";
import type { useSettingsSave } from "@/hooks/use-settings-save";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { promoState, type Promo } from "@/lib/settings/settings";
import { SettingsNotice } from "./SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

export function PromoForm({
  base,
  latest,
  saver,
  busy,
  onSaved,
  onReload,
  onUseLatest,
  onCancel,
}: {
  base: Promo | null;
  latest: Promo | null;
  saver: ReturnType<typeof useSettingsSave<Promo>>;
  busy: boolean;
  onSaved: () => void;
  onReload: () => void;
  onUseLatest: () => void;
  onCancel: () => void;
}) {
  const {
    code,
    discount,
    expiry,
    isActive,
    amount,
    valid,
    uncertain,
    newer,
    changed,
    dirty,
    setCode,
    setDiscount,
    setExpiry,
    setIsActive,
    submit,
    reload,
  } = usePromoForm({ base, latest, saver, onSaved, onReload, onCancel });
  const disabled = busy || saver.busy || uncertain;
  let submitLabel = base ? "Save promo" : "Create promo";
  if (uncertain) {
    submitLabel = "Retry same save";
  }

  return (
    <form
      className={workbench.workspace}
      noValidate
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      {base && (
        <div className={workbench.summary}>
          <div className={workbench.summaryMain}>
            <div>
              <span className={workbench.summaryLabel}>Discount</span>
              <strong className={workbench.summaryValue}>{formatCurrency(base.discount)}</strong>
            </div>
            <div className={workbench.summaryContext}>
              <strong>{promoState(base)}</strong>
              <span>Expires {formatMalaysianDateTime(base.expiresAtUtc)}</span>
            </div>
          </div>
        </div>
      )}
      <div className={workbench.body}>
        <div className={styles.fields}>
          {newer && (
            <div className={styles.notice} role="status">
              <p>A newer version was loaded. Your entries are kept until you use it.</p>
              <Button
                type="button"
                size="compact"
                variant="secondary"
                disabled={disabled}
                onClick={onUseLatest}
              >
                Use latest values
              </Button>
            </div>
          )}
          <section className={styles.section} aria-label="Promo terms">
            <h3>Terms</h3>
            <div className={styles.pair}>
              <Input
                label="Code"
                density="compact"
                value={code}
                maxLength={50}
                disabled={disabled || Boolean(base)}
                onChange={(event) => setCode(event.target.value)}
              />
              <div>
                <Input
                  label="Discount (MYR)"
                  density="compact"
                  inputMode="decimal"
                  value={discount}
                  disabled={disabled}
                  onChange={(event) => setDiscount(event.target.value)}
                />
                <p className={amount === null && discount ? styles.error : styles.hint}>
                  {amount === null && discount
                    ? "Enter an amount with up to two decimals."
                    : "Above zero; the upper limit is checked on save."}
                </p>
              </div>
            </div>
          </section>
          <section className={styles.section} aria-label="Promo validity">
            <h3>Validity</h3>
            <div className={styles.pair}>
              <div>
                <Input
                  label="Expires (Malaysia time)"
                  density="compact"
                  type="datetime-local"
                  value={expiry}
                  disabled={disabled}
                  onChange={(event) => setExpiry(event.target.value)}
                />
                <p className={styles.hint}>The promo stops applying at this exact time.</p>
              </div>
              <label className={styles.check}>
                <input
                  type="checkbox"
                  checked={isActive}
                  disabled={disabled}
                  onChange={(event) => setIsActive(event.target.checked)}
                />
                <span>Active</span>
              </label>
            </div>
          </section>
          {saver.notice && (
            <SettingsNotice
              notice={saver.notice}
              busy={busy || saver.busy}
              reloadable={Boolean(base) || uncertain}
              conflictMessage={
                base
                  ? "This promo changed elsewhere. Reload it to compare; your entries are kept."
                  : "A promo with this code already exists, or the details differ from an earlier attempt."
              }
              reloadLabel={base ? "Reload promo" : "Check saved promo"}
              onReload={reload}
            />
          )}
        </div>
      </div>
      <StaffWorkspaceFooter
        title={base ? "Promo terms" : "New promo"}
        description={
          base
            ? "The code is fixed. Accepted reservations keep the discount they were given."
            : "Codes are case-sensitive and can be reused across separate reservations."
        }
      >
        <ConfirmationButton
          size="compact"
          variant="secondary"
          confirmationTitle="Discard promo changes?"
          message="Your unsaved promo entries will be discarded."
          skipConfirmation={!dirty}
          disabled={saver.busy || Boolean(saver.notice?.uncertain)}
          onConfirm={onCancel}
        >
          {dirty ? "Discard changes" : "Cancel"}
        </ConfirmationButton>
        <Button type="submit" size="compact" disabled={busy || saver.busy || !valid || !changed}>
          {submitLabel}
        </Button>
      </StaffWorkspaceFooter>
    </form>
  );
}
