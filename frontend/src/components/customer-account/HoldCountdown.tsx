"use client";

import { useEffect, useState } from "react";
import { RESERVATION_HOLD_HOURS } from "@/lib/vehicles/deposit-terms";
import styles from "./Reservations.module.css";

const MS_PER_SECOND = 1000;
const SECONDS_PER_HOUR = 3600;
const SECONDS_PER_MINUTE = 60;

function clock(secondsLeft: number) {
  const hours = Math.floor(secondsLeft / SECONDS_PER_HOUR);
  const minutes = Math.floor((secondsLeft % SECONDS_PER_HOUR) / SECONDS_PER_MINUTE);
  const seconds = secondsLeft % SECONDS_PER_MINUTE;
  return [hours, minutes, seconds].map((part) => String(part).padStart(2, "0")).join(":");
}

// The time left on a reservation hold, counted down once a second from the server's deadline.
export function HoldCountdown({ until, label }: { until: string; label?: string }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), MS_PER_SECOND);
    return () => window.clearInterval(timer);
  }, []);
  const secondsLeft = Math.max(0, Math.floor((new Date(until).getTime() - now) / MS_PER_SECOND));
  return (
    <p className={styles.clock}>
      <time className={styles.clockFace}>{clock(secondsLeft)}</time>
      <span>{label ?? `left on your ${RESERVATION_HOLD_HOURS}-hour hold`}</span>
    </p>
  );
}
