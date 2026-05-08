"use client";

import { useEffect, useState } from "react";

// The moment hold timers are read against. It moves on once a minute so a timer counts down and an expired hold
// stops showing time left between reads, without asking the server; callers also set it when they refresh.
export function useMinuteClock() {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, []);
  return [now, setNow] as const;
}
