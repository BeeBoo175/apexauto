"use client";

import { useCallback } from "react";
import { useConfirmedErasure } from "@/hooks/use-confirmed-erasure";
import {
  erasureConflict,
  erasurePath,
  erasurePreviewPath,
  parseErasurePreview,
  type ErasureKind,
} from "@/lib/accounts/account-erasure";

// An Administrator's erasure of a customer or an employee.
export function useAccountErasure(kind: ErasureKind, id: string | null, enabled: boolean) {
  const parsePreview = useCallback((value: unknown) => parseErasurePreview(kind, value), [kind]);
  const erasure = useConfirmedErasure({
    enabled,
    previewPath: id ? erasurePreviewPath(kind, id) : null,
    parsePreview,
    commandPath: id ? erasurePath(kind, id) : null,
    parseConflict: erasureConflict,
  });

  return {
    ...erasure,
    confirm: (reason: string, password: string) => {
      if (reason.trim().length === 0 || password.length === 0) {
        return Promise.resolve();
      }
      return erasure.confirm({ reason, password });
    },
  };
}
