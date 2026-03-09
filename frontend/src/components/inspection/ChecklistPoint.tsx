import { Input, Select, Textarea } from "@/components/ui/Input";
import { label, severities, type Point, type Draft } from "@/lib/inspection/contracts";
import type { ChecklistDraft, EntryDraft } from "@/lib/inspection/draft";
import type { ReportPoint } from "@/lib/inspection/report-contracts";
import type { useInspectionPhoto } from "@/hooks/use-inspection-photo";
import { FindingPhotos } from "./FindingPhotos";
import { SignedFinding } from "./SignedFinding";
import styles from "./Inspection.module.css";
import workshop from "./Workshop.module.css";

export function ChecklistPoint({
  point,
  draft,
  value,
  photo,
  editable,
  disabled,
  onChange,
  signedPoint,
}: {
  point: Point;
  draft: Draft;
  value: ChecklistDraft;
  photo: ReturnType<typeof useInspectionPhoto>;
  editable: boolean;
  disabled: boolean;
  onChange: (v: ChecklistDraft) => void;
  signedPoint?: ReportPoint;
}) {
  const entry = value.entries[point.code];
  const examined = value.examined.includes(point.code);
  function update(next: EntryDraft | null) {
    const entries = { ...value.entries };
    if (next) {
      entries[point.code] = next;
    } else {
      delete entries[point.code];
    }
    onChange({ ...value, entries, examined: value.examined.filter((code) => code !== point.code) });
  }
  const disposition = entry?.kind ?? "Passed";
  return (
    <div className={styles.point}>
      <div className={workshop.detailHeading}>
        <div>
          <span className={workshop.pointCode}>
            {point.code} · {label(point.category)}
          </span>
          <h3>{point.description}</h3>
        </div>
        {editable ? (
          <label className={styles.checkbox}>
            <input
              type="checkbox"
              checked={examined}
              disabled={disabled || entry?.kind === "NotFitted"}
              onChange={(e) =>
                onChange({
                  ...value,
                  examined: e.target.checked
                    ? [...value.examined, point.code]
                    : value.examined.filter((code) => code !== point.code),
                })
              }
            />
            Examined
          </label>
        ) : (
          <span>{label(disposition)}</span>
        )}
      </div>
      {draft.isSigned && signedPoint && (
        <SignedFinding point={signedPoint} inspectionId={draft.id} revision={draft.revision} />
      )}
      {editable && (
        <Select
          label={`${point.code} disposition`}
          density="compact"
          value={disposition}
          disabled={disabled}
          onChange={(e) => {
            if (e.target.value === "Passed") {
              update(null);
              return;
            }
            const kind = e.target.value === "NotFitted" ? "NotFitted" : "Finding";
            update({
              pointCode: point.code,
              kind,
              severity: kind === "Finding" ? "Advisory" : null,
              estimate: "0.00",
              notes: entry?.notes ?? "",
            });
          }}
        >
          <option value="Passed">Passed · no finding</option>
          <option value="Finding">Finding</option>
          {point.description.includes("where fitted") && (
            <option value="NotFitted">Not fitted</option>
          )}
        </Select>
      )}
      {entry && !draft.isSigned && (
        <div className={styles.finding}>
          {editable ? (
            <>
              {entry.kind === "Finding" && (
                <div className={styles.fields}>
                  <Select
                    label={`${point.code} severity`}
                    density="compact"
                    value={entry.severity ?? "Advisory"}
                    disabled={disabled}
                    onChange={(e) =>
                      update({
                        ...entry,
                        severity: severities.find((s) => s === e.target.value) ?? "Advisory",
                      })
                    }
                  >
                    {severities.map((s) => (
                      <option key={s} value={s}>
                        {label(s)}
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={`${point.code} estimate (MYR)`}
                    density="compact"
                    inputMode="decimal"
                    value={entry.estimate}
                    disabled={disabled}
                    onChange={(e) => update({ ...entry, estimate: e.target.value })}
                  />
                </div>
              )}
              <Textarea
                label={`${point.code} ${entry.kind === "NotFitted" ? "absence reason" : "finding notes"}`}
                maxLength={1000}
                rows={2}
                value={entry.notes}
                disabled={disabled}
                onChange={(e) => update({ ...entry, notes: e.target.value })}
              />
            </>
          ) : (
            <p>
              {entry.severity ? `${label(entry.severity)} · MYR ${entry.estimate} · ` : ""}
              {entry.notes}
            </p>
          )}
          {entry.kind === "Finding" && editable && (
            <FindingPhotos
              inspectionId={draft.id}
              pointCode={point.code}
              photo={photo}
              ids={value.photos[point.code] ?? []}
              disabled={disabled}
              onChange={(ids) =>
                onChange({
                  ...value,
                  photos: { ...value.photos, [point.code]: ids },
                  examined: value.examined.filter((code) => code !== point.code),
                })
              }
            />
          )}
        </div>
      )}
    </div>
  );
}
