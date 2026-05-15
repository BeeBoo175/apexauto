"use client";

import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { usePurchaseSettingsForm } from "@/hooks/use-purchase-settings-form";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { PurchaseSettings } from "@/lib/settings/settings";
import type { SettingsDraft } from "@/lib/settings/settings-draft";
import { SettingsNotice } from "./SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

function SettingsField({
  label,
  value,
  hint,
  error,
  mode,
  disabled,
  onChange,
}: {
  label: string;
  value: string;
  hint: string;
  error: string | undefined;
  mode: "decimal" | "numeric";
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div>
      <Input
        label={label}
        density="compact"
        inputMode={mode}
        value={value}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
      <p className={error ? styles.error : styles.hint}>{error ?? hint}</p>
    </div>
  );
}

export function SettingsForm({
  settings,
  draft,
  busy,
  onDraft,
  onSaved,
  onLocked,
}: {
  settings: PurchaseSettings;
  draft: SettingsDraft | null;
  busy: boolean;
  onDraft: (draft: SettingsDraft | null) => void;
  onSaved: (settings: PurchaseSettings) => void;
  onLocked: (locked: boolean) => void;
}) {
  const { saver, current, errors, changed, uncertain, disabled, field, discard, submit, reload } =
    usePurchaseSettingsForm({ settings, draft, busy, onDraft, onSaved, onLocked });
  const { bounds } = settings;
  const maximumPercent = Number((bounds.maximumSstRate * 100).toFixed(2));

  return (
    <form
      className={workbench.workspace}
      noValidate
      aria-busy={saver.busy}
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <LoadingProgress label="Saving settings" active={saver.busy} />
      <div className={workbench.body}>
        <div className={styles.fields}>
          <section className={styles.section} aria-label="Pricing">
            <h3>Pricing</h3>
            <div className={styles.pair}>
              <SettingsField
                label="Processing fee (MYR)"
                mode="decimal"
                value={current.processingFee}
                disabled={disabled}
                error={errors.processingFee}
                hint={`Up to RM ${bounds.maximumFee.toFixed(2)}.`}
                onChange={(value) => field("processingFee", value)}
              />
              <SettingsField
                label="SST rate (%)"
                mode="decimal"
                value={current.sstPercent}
                disabled={disabled}
                error={errors.sstPercent}
                hint={`Applies to the processing fee. Up to ${maximumPercent}%.`}
                onChange={(value) => field("sstPercent", value)}
              />
            </div>
          </section>
          <section className={styles.section} aria-label="Approvals and reservations">
            <h3>Approvals and reservations</h3>
            <div className={styles.pair}>
              <SettingsField
                label="Agent discount limit (MYR)"
                mode="decimal"
                value={current.maxAgentDiscountAmount}
                disabled={disabled}
                error={errors.maxAgentDiscountAmount}
                hint={`Larger negotiated discounts need manager approval. Up to RM ${bounds.maximumDiscount.toFixed(2)}.`}
                onChange={(value) => field("maxAgentDiscountAmount", value)}
              />
              <SettingsField
                label="Reservation hold (hours)"
                mode="numeric"
                value={current.reservationHoldHours}
                disabled={disabled}
                error={errors.reservationHoldHours}
                hint={`Applies to new reservations only (${bounds.minimumHoldHours}–${bounds.maximumHoldHours} hours).`}
                onChange={(value) => field("reservationHoldHours", value)}
              />
            </div>
          </section>
          {saver.notice && (
            <SettingsNotice
              notice={saver.notice}
              busy={busy || saver.busy}
              conflictMessage="These settings changed elsewhere. Your entries are kept until you load the latest values."
              reloadLabel="Load latest settings"
              onReload={reload}
            />
          )}
        </div>
      </div>
      <StaffWorkspaceFooter
        title={changed ? "Unsaved changes" : "Purchase settings"}
        description={`Last changed ${formatMalaysianDateTime(settings.updatedAtUtc)}. Signed documents are not rewritten.`}
      >
        {draft && (
          <Button
            type="button"
            size="compact"
            variant="secondary"
            disabled={disabled}
            onClick={discard}
          >
            Discard changes
          </Button>
        )}
        <Button type="submit" size="compact" disabled={busy || saver.busy || !changed}>
          {uncertain ? "Retry same save" : "Save settings"}
        </Button>
      </StaffWorkspaceFooter>
    </form>
  );
}
