import { remainingHoldSeconds } from "@/lib/sales/contracts";
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

// Paused holds retain their stored time; running holds count down locally between reads.
export function HoldWheel({
  dueAt,
  pausedSeconds,
  now,
  compact = false,
}: {
  dueAt: string | null;
  pausedSeconds: number | null;
  now: number;
  compact?: boolean;
}) {
  const frozen = pausedSeconds !== null;
  const remaining = remainingHoldSeconds({ dueAt, pausedSeconds }, now);
  const state = wheelState(frozen, remaining);
  const hours = Math.floor(remaining / 3600);
  const minutes = Math.floor((remaining % 3600) / 60);
  const suffix = compact && frozen ? "" : " left";
  const note = compact ? "Paused" : wheelNotes[state];
  return (
    <span
      className={`${styles.wheel} ${styles[`wheel${state}`]}`}
      data-compact={compact || undefined}
      title={wheelNotes[state]}
    >
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
          {hours}h {minutes}m{suffix}
        </strong>
        {(!compact || frozen) && <span>{note}</span>}
      </span>
    </span>
  );
}
