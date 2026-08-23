import styles from "./Logo.module.css";

// The ApexAuto mark: an A drawn as a peak with a red chevron inside it, the apex climbing. It takes its colour from
// the surrounding text, so it works on the dark site header and on light surfaces, and the chevron and the word AUTO
// carry the signal red. With markOnly it is the badge used where the navigation is collapsed.
export function Logo({ markOnly = false }: { markOnly?: boolean }) {
  return (
    <span className={styles.logo} data-mark-only={markOnly || undefined}>
      <svg className={styles.mark} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
        <path className={styles.peak} d="M3.5 27 16 5l12.5 22" />
        <path className={styles.chevron} d="m10.2 24.2 5.8-10 5.8 10" />
      </svg>
      {!markOnly && (
        <span className={styles.word}>
          APEX<span className={styles.auto}>AUTO</span>
        </span>
      )}
    </span>
  );
}
