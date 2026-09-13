"use client";

import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useRetentionSettingsForm } from "@/hooks/use-retention-settings-form";
import { formatMalaysianDateTime } from "@/lib/date-format";
import type { RetentionDraft, RetentionSettings } from "@/lib/settings/retention";
import { SettingsNotice } from "./SettingsNotice";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import styles from "@/components/staff/StaffForms.module.css";

function DurationField({
  label,
  value,
  hint,
  error,
  disabled,
  onChange,
}: {
  label: string;
  value: string;
  hint: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div>
      <Input
        label={label}
        density="compact"
        inputMode="numeric"
        value={value}
        disabled={disabled}
        aria-invalid={Boolean(error)}
        onChange={(event) => onChange(event.target.value)}
      />
      <p className={error ? styles.error : styles.hint}>{error ?? hint}</p>
    </div>
  );
}

export function RetentionForm({
  settings,
  draft,
  busy,
  onDraft,
  onSaved,
  onLocked,
}: {
  settings: RetentionSettings;
  draft: RetentionDraft | null;
  busy: boolean;
  onDraft: (draft: RetentionDraft | null) => void;
  onSaved: (settings: RetentionSettings) => void;
  onLocked: (locked: boolean) => void;
}) {
  const form = useRetentionSettingsForm({ settings, draft, busy, onDraft, onSaved, onLocked });
  const { saver, current, errors, changed, shortened, uncertain, disabled } = form;
  const awaitingConfirmation = shortened && !form.shorteningConfirmed;

  return (
    <form
      className={workbench.workspace}
      noValidate
      aria-busy={saver.busy}
      onSubmit={(event) => {
        event.preventDefault();
        void form.submit();
      }}
    >
      <LoadingProgress label="Saving retention settings" active={saver.busy} />
      <div className={workbench.body}>
        <div className={styles.fields}>
          <section className={styles.section} aria-label="Routine clean-up">
            <h3>Routine clean-up</h3>
            <div className={styles.pair}>
              <DurationField
                label="Security records (days)"
                value={current.securityArtifactDays}
                disabled={disabled}
                error={errors.securityArtifactDays}
                hint="Sessions, verification links and password reset links, counted from when they expire or end."
                onChange={(value) => form.field("securityArtifactDays", value)}
              />
              <DurationField
                label="Delivered email details (days)"
                value={current.mailDetailDays}
                disabled={disabled}
                error={errors.mailDetailDays}
                hint="The recipient address kept on delivered notices, counted from delivery."
                onChange={(value) => form.field("mailDetailDays", value)}
              />
            </div>
            <div className={styles.pair}>
              <DurationField
                label="Closed staff invitations (days)"
                value={current.invitationDays}
                disabled={disabled}
                error={errors.invitationDays}
                hint="Invited name, email and mobile, counted from activation, revocation or expiry."
                onChange={(value) => form.field("invitationDays", value)}
              />
            </div>
          </section>
          <section className={styles.section} aria-label="Business records">
            <h3>Business records</h3>
            <div className={styles.pair}>
              <DurationField
                label="Business evidence (years)"
                value={current.businessEvidenceYears}
                disabled={disabled}
                error={errors.businessEvidenceYears}
                hint={`Sales, documents and appointment records keep their personal details this long (${settings.bounds.minimumBusinessYears}–${settings.bounds.maximumBusinessYears} years).`}
                onChange={(value) => form.field("businessEvidenceYears", value)}
              />
            </div>
          </section>
          {shortened && (
            <label className={styles.check}>
              <input
                type="checkbox"
                checked={form.shorteningConfirmed}
                disabled={disabled}
                onChange={(event) => form.confirmShortening(event.target.checked)}
              />
              Shortening a period can remove details from records that are already older than the
              new period. I understand.
            </label>
          )}
          {saver.notice && (
            <SettingsNotice
              notice={saver.notice}
              busy={busy || saver.busy}
              conflictMessage="These settings changed elsewhere. Your entries are kept until you load the latest values."
              reloadLabel="Load latest settings"
              onReload={form.reload}
            />
          )}
        </div>
      </div>
      <StaffWorkspaceFooter
        title={changed ? "Unsaved changes" : "Retention settings"}
        description={`Last changed ${formatMalaysianDateTime(settings.updatedAtUtc)}. A change applies to records that are not yet cleaned up.`}
      >
        {draft && (
          <Button
            type="button"
            size="compact"
            variant="secondary"
            disabled={disabled}
            onClick={form.discard}
          >
            Discard changes
          </Button>
        )}
        <Button
          type="submit"
          size="compact"
          disabled={busy || saver.busy || !changed || awaitingConfirmation}
        >
          {uncertain ? "Retry same save" : "Save settings"}
        </Button>
      </StaffWorkspaceFooter>
    </form>
  );
}
