import patterns from "@/components/layout/PublicPatterns.module.css";
import styles from "./VehicleGradeBadge.module.css";

// The grade is a shortcut to the signed inspection report it comes from.
export function VehicleGradeBadge({ grade, onSelect }: { grade: string; onSelect: () => void }) {
  return (
    <button
      type="button"
      className={`${patterns.tag} ${styles.badge}`}
      aria-label={`Inspection grade ${grade}, open the inspection report`}
      onClick={onSelect}
    >
      <span>Grade {grade}</span>
      <span className={styles.hint}>Inspection report</span>
    </button>
  );
}
