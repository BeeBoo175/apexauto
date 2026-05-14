"use client";

import { useState } from "react";
import { useEditGuard } from "@/hooks/use-edit-guard";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import type { useSettingsSave } from "@/hooks/use-settings-save";
import { parseMyr } from "@/lib/currency-input";
import { formatCurrency } from "@/lib/currency-format";
import { formatMalaysianDateTime } from "@/lib/date-format";
import { recordPath, type Promo } from "@/lib/settings/settings";
import { malaysiaInput, utcFromMalaysiaInput } from "@/lib/settings/settings-draft";
import { SettingsNotice } from "./SettingsNotice";
import { promoState } from "./settings-columns";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

// The draft is based on the record as it was opened; a newer read never replaces it silently.
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
  const toast = useToast();
  // One identity per open create drawer lets a lost response be retried without a duplicate.
  const [id] = useState(() => base?.id ?? crypto.randomUUID());
  const [code, setCode] = useState(base?.code ?? "");
  const [discount, setDiscount] = useState(base ? base.discount.toFixed(2) : "");
  const [expiry, setExpiry] = useState(base ? malaysiaInput(base.expiresAtUtc) : "");
  const [isActive, setIsActive] = useState(base?.isActive ?? true);
  const amount = parseMyr(discount.trim());
  const expiresAtUtc = utcFromMalaysiaInput(expiry);
  const valid = Boolean(code.trim()) && amount !== null && amount > 0 && expiresAtUtc !== null;
  const uncertain = Boolean(saver.notice?.uncertain);
  const disabled = busy || saver.busy || uncertain;
  const newer = base && latest && latest.version !== base.version;
  // Like the other editors, an existing record can be saved only once something differs from it.
  const changed =
    !base ||
    amount !== base.discount ||
    expiresAtUtc !== new Date(base.expiresAtUtc).toISOString() ||
    isActive !== base.isActive;
  const dirty = base ? changed : Boolean(code || discount || expiry || !isActive);
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain: Boolean(saver.notice?.uncertain),
    label: "Promo",
    discard: onCancel,
  });
  let submitLabel = base ? "Save promo" : "Create promo";
  if (uncertain) {
    submitLabel = "Retry same save";
  }

  async function submit() {
    if (!valid || amount === null || expiresAtUtc === null) {
      return;
    }
    const saved = base
      ? await saver.save(recordPath("promos", base.id), "PUT", {
          discount: amount,
          expiresAtUtc,
          isActive,
          expectedVersion: base.version,
        })
      : await saver.save("/api/staff/settings/promos", "POST", {
          id,
          code: code.trim(),
          discount: amount,
          expiresAtUtc,
          isActive,
        });
    if (saved) {
      toast.success(base ? "Promo saved." : "Promo created.");
      onSaved();
    }
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
              onReload={async () => {
                const latest = await saver.reload(recordPath("promos", id));
                if (latest.status === "loaded") {
                  if (base) {
                    onReload();
                  } else {
                    onSaved();
                  }
                } else if (latest.status === "missing") {
                  toast.info("This promo was not created. Review your entries or discard them.");
                } else {
                  toast.error(
                    "The promo could not be checked. Your entries and recovery state are kept.",
                  );
                }
              }}
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
