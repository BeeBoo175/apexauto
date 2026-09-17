import { Icon } from "@/components/ui/Icon";
import styles from "./Sell.module.css";

export const SELLER_STEPS = ["Vehicle details", "Valuation", "Exterior photos", "Appointment"];

const stepNotes = [
  "Specs and plate",
  "Indicative RM range",
  "Four exterior angles",
  "90-min branch bay",
] as const;

export function SellerProgress({ step }: { step: number }) {
  return (
    <div className={styles.progressBand}>
      <div className={styles.progressWrap}>
        <p className={styles.progressSummary}>
          <strong>
            Step {step + 1} of {SELLER_STEPS.length}
          </strong>
          <span>{SELLER_STEPS[step]}</span>
        </p>
        <ol className={styles.progress}>
          {SELLER_STEPS.map((label, index) => {
            const done = index < step;
            const current = index === step;
            return (
              <li
                key={label}
                aria-current={current ? "step" : undefined}
                data-done={done || undefined}
              >
                <span className={styles.stepMarker} aria-hidden="true">
                  {done ? <Icon name="check" /> : `0${index + 1}`}
                </span>
                <div className={styles.stepText}>
                  <span className={styles.stepTitle}>{label}</span>
                  <span className={styles.stepNote}>{stepNotes[index]}</span>
                </div>
              </li>
            );
          })}
        </ol>
      </div>
    </div>
  );
}
