"use client";

import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { StaffRecordSummary } from "@/components/staff/StaffRecordSummary";
import { StaffStatusGroup } from "@/components/staff/StaffStatusGroup";
import tones from "@/components/ui/StatusTone.module.css";
import { Input, Select } from "@/components/ui/Input";
import type { useSettingsSave } from "@/hooks/use-settings-save";
import { useWarrantyForm } from "@/hooks/use-warranty-form";
import { formatCurrency } from "@/lib/currency-format";
import { warrantyGrades, type Warranty, type WarrantyGrade } from "@/lib/settings/settings";
import { SettingsNotice } from "./SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

export function WarrantyForm({
  base,
  latest,
  saver,
  busy,
  onSaved,
  onReload,
  onUseLatest,
  onCancel,
}: {
  base: Warranty | null;
  latest: Warranty | null;
  saver: ReturnType<typeof useSettingsSave<Warranty>>;
  busy: boolean;
  onSaved: () => void;
  onReload: () => void;
  onUseLatest: () => void;
  onCancel: () => void;
}) {
  const {
    name,
    months,
    kilometres,
    price,
    grade,
    isActive,
    amount,
    valid,
    uncertain,
    newer,
    changed,
    dirty,
    setName,
    setMonths,
    setKilometres,
    setPrice,
    setGrade,
    setIsActive,
    submit,
    reload,
  } = useWarrantyForm({ base, latest, saver, onSaved, onReload, onCancel });
  const disabled = busy || saver.busy || uncertain;
  let submitLabel = base ? "Save package" : "Create package";
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
        <StaffRecordSummary
          ariaLabel="Warranty price and status"
          label="Price"
          value={formatCurrency(base.price)}
          status={
            <StaffStatusGroup label="Warranty status">
              <Badge className={`${tones.badge} ${base.isActive ? tones.success : tones.neutral}`}>
                {base.isActive ? "Active" : "Inactive"}
              </Badge>
            </StaffStatusGroup>
          }
          context={
            <span>
              Grade {base.minimumGrade} or better · {base.durationMonths} months
            </span>
          }
        />
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
          <section className={styles.section} aria-label="Package">
            <h3>Package</h3>
            <Input
              label="Package name"
              density="compact"
              value={name}
              maxLength={100}
              disabled={disabled}
              onChange={(event) => setName(event.target.value)}
            />
          </section>
          <section className={styles.section} aria-label="Coverage">
            <h3>Coverage</h3>
            <div className={styles.pair}>
              <Input
                label="Term (months)"
                density="compact"
                inputMode="numeric"
                value={months}
                disabled={disabled}
                onChange={(event) => setMonths(event.target.value)}
              />
              <Input
                label="Mileage cap (km)"
                density="compact"
                inputMode="numeric"
                value={kilometres}
                disabled={disabled}
                onChange={(event) => setKilometres(event.target.value)}
              />
            </div>
          </section>
          <section className={styles.section} aria-label="Pricing and eligibility">
            <h3>Pricing and eligibility</h3>
            <div className={styles.pair}>
              <div>
                <Input
                  label="Price (MYR)"
                  density="compact"
                  inputMode="decimal"
                  value={price}
                  disabled={disabled}
                  onChange={(event) => setPrice(event.target.value)}
                />
                <p className={amount === null && price ? styles.error : styles.hint}>
                  {amount === null && price
                    ? "Enter an amount with up to two decimals."
                    : "Limits are checked on save."}
                </p>
              </div>
              <Select
                label="Minimum vehicle grade"
                density="compact"
                value={grade}
                disabled={disabled}
                onChange={(event) => setGrade(event.target.value as WarrantyGrade)}
              >
                {warrantyGrades.map((option) => (
                  <option key={option} value={option}>
                    Grade {option} or better
                  </option>
                ))}
              </Select>
            </div>
            <label className={styles.check}>
              <input
                type="checkbox"
                checked={isActive}
                disabled={disabled}
                onChange={(event) => setIsActive(event.target.checked)}
              />
              <span>Active (offered on new deals)</span>
            </label>
          </section>
          {saver.notice && (
            <SettingsNotice
              notice={saver.notice}
              busy={busy || saver.busy}
              reloadable={Boolean(base) || uncertain}
              conflictMessage={
                base
                  ? "This package changed elsewhere, or another package uses this name. Reload it to compare; your entries are kept."
                  : "A package with this name already exists, or the details differ from an earlier attempt."
              }
              reloadLabel={base ? "Reload package" : "Check saved package"}
              onReload={reload}
            />
          )}
        </div>
      </div>
      <StaffWorkspaceFooter
        title={base ? "Package terms" : "New warranty package"}
        description="Sold deals keep their original terms."
      >
        <ConfirmationButton
          size="compact"
          variant="secondary"
          confirmationTitle="Discard package changes?"
          message="Your unsaved warranty package entries will be discarded."
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
