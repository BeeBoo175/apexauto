"use client";

import { useEffect, useState } from "react";
import { privateApiFetch } from "@/lib/api-browser";
import {
  accountBlockersPath,
  parseBlockers,
  type AccountBlocker,
} from "@/lib/accounts/account-administration";

// The open work an employee owns, read each time a deactivation or transfer dialog opens: it must be current,
// so it never comes from the reused snapshots, and the server rechecks it when the change is confirmed.
export function useAccountBlockers(id: string, enabled: boolean) {
  const [state, setState] = useState<{
    id: string;
    blockers: AccountBlocker[] | null;
    failed: boolean;
  } | null>(null);

  useEffect(() => {
    if (!enabled || !id) {
      return;
    }
    const attempt = new AbortController();
    privateApiFetch(
      accountBlockersPath(id),
      parseBlockers,
      { signal: attempt.signal },
      { fresh: true },
    )
      .then((result) => {
        if (!attempt.signal.aborted) {
          const blockers = result.status === 200 ? result.data : null;
          setState({ id, blockers, failed: blockers === null });
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setState({ id, blockers: null, failed: true });
        }
      });
    return () => attempt.abort();
  }, [id, enabled]);

  const current = enabled && state?.id === id ? state : null;
  return {
    blockers: current?.blockers ?? null,
    loading: enabled && current === null,
    failed: current?.failed ?? false,
  };
}
