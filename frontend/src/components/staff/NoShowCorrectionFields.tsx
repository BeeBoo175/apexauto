import { Input, Textarea } from "@/components/ui/Input";
import type { CheckInDraft } from "@/lib/staff/contracts";
import styles from "./AppointmentWorkbench.module.css";

export function NoShowCorrectionFields({
  draft,
  onChange,
  disabled,
}: {
  draft: CheckInDraft;
  onChange: (draft: CheckInDraft) => void;
  disabled: boolean;
}) {
  return (
    <fieldset disabled={disabled} className={styles.fields}>
      <legend className="sr-only">Attendance correction</legend>
      <Input
        label="Actual attendance time (Malaysia)"
        type="time"
        required
        value={draft.attendedTime ?? ""}
        onChange={(event) => onChange({ ...draft, attendedTime: event.target.value })}
      />
      <Textarea
        label="Correction reason"
        required
        rows={3}
        maxLength={1000}
        value={draft.correctionReason ?? ""}
        onChange={(event) => onChange({ ...draft, correctionReason: event.target.value })}
      />
    </fieldset>
  );
}
