import { remainingHoldSeconds } from "@/lib/sales/contracts";
import styles from "./HoldTimer.module.css";

type TimerState = "Frozen" | "Red" | "Amber" | "Open";

// Amber in the last half of the hold and red in the last eighth of its original duration.
function timerState(frozen: boolean, remaining: number, total: number): TimerState {
  if (frozen) {
    return "Frozen";
  }
  if (remaining < total / 8) {
    return "Red";
  }
  return remaining < total / 2 ? "Amber" : "Open";
}

const timerNotes: Record<TimerState, string> = {
  Frozen: "Paused while claimed",
  Red: "Expires soon",
  Amber: "Until the hold expires",
  Open: "Until the hold expires",
};

// A ring and the time left for a hold. Paused holds retain their stored time; running holds count down locally
// between reads. Shared by sale holds and deposit requests.
export function HoldTimer({
  dueAt,
  pausedSeconds = null,
  now,
  totalSeconds,
  compact = false,
}: {
  dueAt: string | null;
  pausedSeconds?: number | null;
  now: number;
  totalSeconds: number;
  compact?: boolean;
}) {
  const frozen = pausedSeconds !== null;
  const remaining = remainingHoldSeconds({ dueAt, pausedSeconds }, now);
  const state = timerState(frozen, remaining, totalSeconds);
  const hours = Math.floor(remaining / 3600);
  const minutes = Math.floor((remaining % 3600) / 60);
  const suffix = compact && frozen ? "" : " left";
  const note = compact ? "Paused" : timerNotes[state];
  return (
    <span
      className={`${styles.timer} ${styles[`timer${state}`]}`}
      data-compact={compact || undefined}
      title={timerNotes[state]}
    >
      <svg className={styles.ring} viewBox="0 0 36 36" aria-hidden="true">
        <circle className={styles.track} cx="18" cy="18" r="16" />
        <circle
          className={styles.arc}
          cx="18"
          cy="18"
          r="16"
          pathLength="100"
          strokeDasharray={`${Math.min(1, remaining / totalSeconds) * 100} 100`}
        />
      </svg>
      <span className={styles.text}>
        <strong>
          {hours}h {minutes}m{suffix}
        </strong>
        {(!compact || frozen) && <span>{note}</span>}
      </span>
    </span>
  );
}
