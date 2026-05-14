"use client";

import { useState } from "react";
import { useEditGuard } from "@/hooks/use-edit-guard";
import { ConfirmationButton } from "@/components/ui/ConfirmationButton";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Input, Select } from "@/components/ui/Input";
import { useToast } from "@/components/ui/Toast";
import type { useSettingsSave } from "@/hooks/use-settings-save";
import { parseMyr } from "@/lib/currency-input";
import { formatCurrency } from "@/lib/currency-format";
import {
  recordPath,
  warrantyGrades,
  type Warranty,
  type WarrantyGrade,
} from "@/lib/settings/settings";
import { SettingsNotice } from "./SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

const whole = (value: string) => (/^\d+$/.test(value.trim()) ? Number(value.trim()) : null);

// The draft is based on the record as it was opened; a newer read never replaces it silently.
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
  const toast = useToast();
  // One identity per open create drawer lets a lost response be retried without a duplicate.
  const [id] = useState(() => base?.id ?? crypto.randomUUID());
  const [name, setName] = useState(base?.name ?? "");
  const [months, setMonths] = useState(base ? String(base.durationMonths) : "");
  const [kilometres, setKilometres] = useState(base ? String(base.mileageCapKm) : "");
  const [price, setPrice] = useState(base ? base.price.toFixed(2) : "");
  const [grade, setGrade] = useState<WarrantyGrade>(base?.minimumGrade ?? "C");
  const [isActive, setIsActive] = useState(base?.isActive ?? true);
  const durationMonths = whole(months);
  const mileageCapKm = whole(kilometres);
  const amount = parseMyr(price.trim());
  const valid =
    Boolean(name.trim()) &&
    durationMonths !== null &&
    durationMonths > 0 &&
    mileageCapKm !== null &&
    mileageCapKm > 0 &&
    amount !== null;
  const uncertain = Boolean(saver.notice?.uncertain);
  const disabled = busy || saver.busy || uncertain;
  const newer = base && latest && latest.version !== base.version;
  // Like the other editors, an existing record can be saved only once something differs from it.
  const changed =
    !base ||
    name.trim() !== base.name ||
    durationMonths !== base.durationMonths ||
    mileageCapKm !== base.mileageCapKm ||
    amount !== base.price ||
    grade !== base.minimumGrade ||
    isActive !== base.isActive;
  const dirty = base
    ? changed
    : Boolean(name || months || kilometres || price || grade !== "C" || !isActive);
  useEditGuard({
    dirty,
    pending: saver.busy,
    uncertain: Boolean(saver.notice?.uncertain),
    label: "Warranty package",
    discard: onCancel,
  });
  let submitLabel = base ? "Save package" : "Create package";
  if (uncertain) {
    submitLabel = "Retry same save";
  }

  async function submit() {
    if (!valid || durationMonths === null || mileageCapKm === null || amount === null) {
      return;
    }
    const terms = {
      name: name.trim(),
      durationMonths,
      mileageCapKm,
      price: amount,
      minimumGrade: grade,
      isActive,
    };
    const saved = base
      ? await saver.save(recordPath("warranties", base.id), "PUT", {
          ...terms,
          expectedVersion: base.version,
        })
      : await saver.save("/api/staff/settings/warranties", "POST", { id, ...terms });
    if (saved) {
      toast.success(base ? "Warranty package saved." : "Warranty package created.");
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
              <span className={workbench.summaryLabel}>Price</span>
              <strong className={workbench.summaryValue}>{formatCurrency(base.price)}</strong>
            </div>
            <div className={workbench.summaryContext}>
              <strong>{base.isActive ? "Active" : "Inactive"}</strong>
              <span>
                Grade {base.minimumGrade} or better · {base.durationMonths} months
              </span>
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
              onReload={async () => {
                const latest = await saver.reload(recordPath("warranties", id));
                if (latest.status === "loaded") {
                  if (base) {
                    onReload();
                  } else {
                    onSaved();
                  }
                } else if (latest.status === "missing") {
                  toast.info("This package was not created. Review your entries or discard them.");
                } else {
                  toast.error(
                    "The package could not be checked. Your entries and recovery state are kept.",
                  );
                }
              }}
            />
          )}
        </div>
      </div>
      <StaffWorkspaceFooter
        title={base ? "Package terms" : "New warranty package"}
        description="Existing deals and certificates keep the terms they were sold with."
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
