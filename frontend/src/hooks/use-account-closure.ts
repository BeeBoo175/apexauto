"use client";

import { useState } from "react";
import { useConfirmedErasure } from "@/hooks/use-confirmed-erasure";
import {
  CUSTOMER_CLOSURE_BLOCKERS_PATH,
  CUSTOMER_CLOSURE_PATH,
  closureConflict,
  parseClosurePreview,
} from "@/lib/accounts/customer-closure";

// A customer closing their own account. Replaying a confirmation that had already happened meets a closed session
// (401), which is reported as closed rather than as an error.
export function useAccountClosure(enabled: boolean) {
  const [accessClosed, setAccessClosed] = useState(false);
  const erasure = useConfirmedErasure({
    enabled,
    previewPath: CUSTOMER_CLOSURE_BLOCKERS_PATH,
    parsePreview: parseClosurePreview,
    commandPath: CUSTOMER_CLOSURE_PATH,
    parseConflict: closureConflict,
    onSessionLost: () => setAccessClosed(true),
  });

  return {
    state: erasure.state,
    busy: erasure.busy,
    uncertain: erasure.uncertain,
    problem: erasure.problem,
    receipt: erasure.receipt,
    accessClosed,
    clearProblem: erasure.clearProblem,
    reload: erasure.reload,
    confirm: (password: string) => {
      if (password.length === 0) {
        return Promise.resolve();
      }
      return erasure.confirm({ password });
    },
  };
}
