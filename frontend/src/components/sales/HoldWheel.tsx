import styles from "./Sales.module.css";

const HOLD_SECONDS = 48 * 3600;
const AMBER_SECONDS = 24 * 3600;
const RED_SECONDS = 6 * 3600;

type WheelState = "Frozen" | "Red" | "Amber" | "Open";

function wheelState(frozen: boolean, remaining: number): WheelState {
  if (frozen) {
    return "Frozen";
  }
  if (remaining < RED_SECONDS) {
    return "Red";
  }
  return remaining < AMBER_SECONDS ? "Amber" : "Open";
}

const wheelNotes: Record<WheelState, string> = {
  Frozen: "Paused while claimed",
  Red: "Expires soon",
  Amber: "Until the hold expires",
  Open: "Until the hold expires",
};

// The hold's time as a ring and always as text: green, amber under 24 hours, red under 6, teal while an agent has it.
export function HoldWheel({
  dueAt,
  pausedSeconds,
  now,
}: {
  dueAt: string | null;
  pausedSeconds: number | null;
  now: number;
}) {
  const frozen = pausedSeconds !== null;
  const remaining = frozen
    ? pausedSeconds
    : Math.max(0, Math.floor((Date.parse(dueAt ?? "") - now) / 1000) || 0);
  const state = wheelState(frozen, remaining);
  const hours = Math.floor(remaining / 3600);
  const minutes = Math.floor((remaining % 3600) / 60);
  return (
    <span className={`${styles.wheel} ${styles[`wheel${state}`]}`}>
      <svg className={styles.wheelRing} viewBox="0 0 36 36" aria-hidden="true">
        <circle className={styles.wheelTrack} cx="18" cy="18" r="16" />
        <circle
          className={styles.wheelArc}
          cx="18"
          cy="18"
          r="16"
          pathLength="100"
          strokeDasharray={`${Math.min(1, remaining / HOLD_SECONDS) * 100} 100`}
        />
      </svg>
      <span className={styles.wheelText}>
        <strong>
          {hours}h {minutes}m left
        </strong>
        <span>{wheelNotes[state]}</span>
      </span>
    </span>
  );
}
