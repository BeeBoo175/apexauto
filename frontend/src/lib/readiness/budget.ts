// A fixed-window budget for how often this server instance will probe the API. Every probe can wake a free-tier
// service, so callers beyond the budget wait instead.

export function createProbeBudget(limit: number, windowMs: number) {
  let windowStart = 0;
  let used = 0;

  return function admit(
    now: number,
  ): { admitted: true } | { admitted: false; retryAfterSeconds: number } {
    if (now - windowStart >= windowMs) {
      windowStart = now;
      used = 0;
    }
    if (used >= limit) {
      return {
        admitted: false,
        retryAfterSeconds: Math.ceil((windowStart + windowMs - now) / 1000),
      };
    }
    used += 1;
    return { admitted: true };
  };
}
